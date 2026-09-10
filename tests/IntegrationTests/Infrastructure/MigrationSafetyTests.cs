using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class MigrationSafetyTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task FreshMigration_ShouldCreateNonNullableAttachmentMalwareScanStateDefaultingFalse()
    {
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT is_nullable, column_default FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'document_attachments' AND column_name = 'malware_scan_clean'",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        (await reader.ReadAsync()).ShouldBeTrue();
        reader.GetString(0).ShouldBe("NO");
        reader.GetString(1).ShouldContain("false", Case.Insensitive);
    }

    [Fact]
    public async Task VersionBoundedNonIdempotentMigrationScript_ShouldRunConcurrentIndexCommandsOutsideTransaction_AndRecordHistoryAfterward()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IMigrator migrator = context.GetService<IMigrator>();

        string script = migrator.GenerateScript(
            fromMigration: "20260830165354_ImplementPhase10UserAdministration",
            toMigration: "20260910000000_ValidateTrigramIndexReadiness",
            options: MigrationsSqlGenerationOptions.Default);
        const string firstConcurrentIndex = "CREATE INDEX CONCURRENTLY ix_materials_code_trgm";
        int firstConcurrentIndexOffset = script.IndexOf(firstConcurrentIndex, StringComparison.Ordinal);

        firstConcurrentIndexOffset.ShouldBeGreaterThanOrEqualTo(0);
        script.ShouldNotContain("CREATE INDEX CONCURRENTLY IF NOT EXISTS", Case.Insensitive);
        script[..firstConcurrentIndexOffset].TrimEnd().ShouldEndWith("COMMIT;");
        Should.NotThrow(() => MigrationDeploymentScriptValidator.ValidateConcurrentIndexDeploymentScript(script));
    }

    [Fact]
    public async Task IdempotentMigrationScript_ShouldBeRejectedWhenItWrapsConcurrentIndexDdlInEfDoBlock()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IMigrator migrator = context.GetService<IMigrator>();

        string script = migrator.GenerateScript(
            fromMigration: "20260830165354_ImplementPhase10UserAdministration",
            toMigration: "20260910000000_ValidateTrigramIndexReadiness",
            options: MigrationsSqlGenerationOptions.Idempotent);

        script.ShouldContain("DO $EF$");
        script.ShouldContain("CREATE INDEX CONCURRENTLY");
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            MigrationDeploymentScriptValidator.ValidateConcurrentIndexDeploymentScript(script));
        exception.Message.ShouldContain("idempotent", Case.Insensitive);
    }

    [Fact]
    public void DeploymentScripts_ShouldFailClosedForUnhealthyIndexes_AndKeepOneLockAcrossMigrationExecution()
    {
        string repositoryRoot = FindRepositoryRoot();
        string preflight = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "migration-preflight.sql"));
        string runner = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "run-reviewed-migration.sql"));
        string runnerWrapper = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "run-reviewed-migration.sh"));
        string preflightWrapper = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "run-migration-preflight.sh"));

        preflight.ShouldContain("AS unhealthy_indexes \\gset");
        preflight.ShouldContain("\\if :unhealthy_indexes");
        preflight.ShouldContain("EIAMS_PREFLIGHT_UNHEALTHY_INDEXES");
        runner.ShouldContain("pg_try_advisory_lock(hashtextextended('eiams:ef-migrations', 0))");
        runner.ShouldContain("\\i :migration_script");
        runner.ShouldContain("EIAMS_RUNNER_UNHEALTHY_INDEXES");
        runnerWrapper.ShouldContain("DO \\$EF\\$", Case.Insensitive);
        preflightWrapper.ShouldContain("exit 5");

        Should.Throw<InvalidOperationException>(() =>
            MigrationDeploymentScriptValidator.ValidateConcurrentIndexDeploymentScript(
                "do $ef$ begin create index concurrently sample on public.sample (id); end $ef$;"));
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "All relation and function identifiers are generated from Guid hexadecimal text and used only in the disposable Testcontainers database.")]
    public async Task CancelledConcurrentIndexBuild_ShouldLeaveDetectableInvalidIndex_AndFixtureCleanupShouldRemoveIt()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string table = $"migration_index_cancel_{suffix}";
        string function = $"migration_slow_identity_{suffix}";
        string index = $"migration_invalid_index_{suffix}";

        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();

        try
        {
            await ExecuteAsync(connection, $"CREATE TABLE public.\"{table}\" (value integer NOT NULL)");
            await ExecuteAsync(connection, $"INSERT INTO public.\"{table}\" (value) SELECT generate_series(1, 1000)");
            await ExecuteAsync(connection, $"""
                CREATE FUNCTION public."{function}"(input integer)
                RETURNS integer
                LANGUAGE plpgsql
                IMMUTABLE
                AS $body$
                BEGIN
                    PERFORM pg_sleep(0.01);
                    RETURN input;
                END;
                $body$
                """);
            await ExecuteAsync(connection, "SET statement_timeout = '100ms'");

            PostgresException exception = await Should.ThrowAsync<PostgresException>(() =>
                ExecuteAsync(connection,
                    $"CREATE INDEX CONCURRENTLY \"{index}\" ON public.\"{table}\" (public.\"{function}\"(value))"));

            exception.SqlState.ShouldBe(PostgresErrorCodes.QueryCanceled);
            await ExecuteAsync(connection, "SET statement_timeout = 0");

            await using var invalidIndexQuery = new NpgsqlCommand(
                """
                SELECT NOT i.indisvalid OR NOT i.indisready
                FROM pg_catalog.pg_index AS i
                INNER JOIN pg_catalog.pg_class AS c ON c.oid = i.indexrelid
                WHERE c.oid = to_regclass(@index_name)
                """,
                connection);
            invalidIndexQuery.Parameters.AddWithValue("index_name", $"public.{index}");

            (await invalidIndexQuery.ExecuteScalarAsync()).ShouldBe(true);
        }
        finally
        {
            await ExecuteAsync(connection, "SET statement_timeout = 0");
            await ExecuteAsync(connection, $"DROP INDEX CONCURRENTLY IF EXISTS public.\"{index}\"");
            await ExecuteAsync(connection, $"DROP FUNCTION IF EXISTS public.\"{function}\"(integer)");
            await ExecuteAsync(connection, $"DROP TABLE IF EXISTS public.\"{table}\"");
        }

        await using var existenceQuery = new NpgsqlCommand("SELECT to_regclass(@relation_name)::text", connection);
        existenceQuery.Parameters.AddWithValue("relation_name", $"public.{index}");
        (await existenceQuery.ExecuteScalarAsync() is null or DBNull).ShouldBeTrue();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "This helper is called only by the preceding disposable-Testcontainers fixture, whose dynamic identifiers are generated from Guid hexadecimal text.")]
    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection)
        {
            CommandTimeout = 15
        };
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CleanArchitectureTemplate.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for migration-script verification.");
    }
}
