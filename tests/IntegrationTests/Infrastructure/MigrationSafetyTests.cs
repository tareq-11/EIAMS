using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class MigrationSafetyTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public void PolymorphicHolderMigration_LocksReferenceTablesBeforePreflight()
    {
        string migrationPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Infrastructure",
            "Migrations",
            "20260927103000_EnforcePolymorphicHolderTargets.cs");
        string migration = File.ReadAllText(migrationPath);
        int lockIndex = migration.IndexOf("LOCK TABLE public.custodies", StringComparison.Ordinal);
        int preflightIndex = migration.IndexOf("DO $$", lockIndex, StringComparison.Ordinal);

        lockIndex.ShouldBeGreaterThanOrEqualTo(0);
        preflightIndex.ShouldBeGreaterThan(lockIndex);
        migration[..preflightIndex].ShouldContain("public.durable_custody_allocations");
        migration[..preflightIndex].ShouldContain("public.tracked_material_units");
        migration[..preflightIndex].ShouldContain("public.issue_to");
        migration[..preflightIndex].ShouldContain("public.employees");
        migration[..preflightIndex].ShouldContain("public.external_parties");
        migration[..preflightIndex].ShouldContain("public.organizational_units");
        migration[..preflightIndex].ShouldContain("public.sites");
        migration[..preflightIndex].ShouldContain("IN SHARE ROW EXCLUSIVE MODE");
    }

    [Fact]
    public async Task CutoverMigration_OnFreshDisposableDatabase_ShouldApplyAndRejectLegacyScope()
    {
        string database = $"migration_1d_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            (await ScalarAsync(connection, "SELECT COUNT(*) FROM public.permissions WHERE code LIKE '%:%'")).ShouldBe(0);
            (await ScalarAsync(connection, "SELECT COUNT(*) FROM public.role_permissions rp JOIN public.permissions p ON p.id = rp.permission_id WHERE p.code LIKE '%:%'")).ShouldBe(0);
            (await ScalarAsync(connection, "SELECT active_vocabulary FROM public.authorization_policy_versions WHERE is_active")).ShouldBe("dotted-v1");
            await ExecuteAsync(connection, $"INSERT INTO public.users (id, email, username, first_name, last_name, password_hash, status, created_at_utc) VALUES ('{Guid.NewGuid()}', 'migration-{database}@example.test', 'migration-{database}', 'Migration', 'Test', 'hash', 'Active', NOW())");
            var userId = Guid.NewGuid();
            await ExecuteAsync(connection, $"INSERT INTO public.users (id, email, username, first_name, last_name, password_hash, status, created_at_utc) VALUES ('{userId}', 'scope-{database}@example.test', 'scope-{database}', 'Scope', 'Test', 'hash', 'Active', NOW())");
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(connection, $"INSERT INTO public.user_role_scopes (id, user_id, role_id, scope_type, scope_id, row_version, created_at_utc) VALUES ('{Guid.NewGuid()}', '{userId}', '{Domain.Roles.WellKnownRoles.AdministratorId}', 'OrganizationalUnit', '{Guid.NewGuid()}', 1, NOW())"));

            (await ScalarAsync(connection, "SELECT COUNT(*) FROM public.role_allowed_scope_types WHERE scope_type = 'OrganizationalUnit'")).ShouldBe(0);
            (await ScalarAsync(connection, "SELECT COUNT(*) FROM public.permission_allowed_scope_types WHERE scope_type = 'OrganizationalUnit'")).ShouldBe(0);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task CutoverMigration_WithLegacyAssignment_ShouldFailBeforeHistoryOrMutation()
    {
        string database = $"migration_1d_guard_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync("20260922012720_AddUserRoleScopeRowVersion");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            var userId = Guid.NewGuid();
            var assignmentId = Guid.NewGuid();
            await ExecuteAsync(connection, $"INSERT INTO public.users (id, email, username, first_name, last_name, password_hash, status, created_at_utc) VALUES ('{userId}', 'legacy-{database}@example.test', 'legacy-{database}', 'Legacy', 'Test', 'hash', 'Active', NOW())");
            await ExecuteAsync(connection, $"INSERT INTO public.user_role_scopes (id, user_id, role_id, scope_type, scope_id, row_version, created_at_utc) VALUES ('{assignmentId}', '{userId}', '{Domain.Roles.WellKnownRoles.WarehouseManagerId}', 'OrganizationalUnit', '{Guid.NewGuid()}', 1, NOW())");

            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync());
            (await ScalarAsync(connection, $"SELECT COUNT(*) FROM public.user_role_scopes WHERE id = '{assignmentId}'")).ShouldBe(1);
            (await ScalarAsync(connection, "SELECT COUNT(*) FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260922021122_CutoverUserAssignmentScopeVocabulary'")).ShouldBe(0);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task CutoverMigration_WithZeroAssignmentUser_ShouldFailBeforeHistory()
    {
        string database = $"migration_1d_zero_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync("20260922012720_AddUserRoleScopeRowVersion");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"INSERT INTO public.users (id, email, username, first_name, last_name, password_hash, status, created_at_utc) VALUES ('{Guid.NewGuid()}', 'zero-{database}@example.test', 'zero-{database}', 'Zero', 'Assignment', 'hash', 'Active', NOW())");

            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync());
            (await ScalarAsync(connection, "SELECT COUNT(*) FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260922021122_CutoverUserAssignmentScopeVocabulary'")).ShouldBe(0);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task CutoverMigration_WithSuspendedZeroAssignmentUser_ShouldSucceed()
    {
        string database = $"migration_1d_suspended_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync("20260922012720_AddUserRoleScopeRowVersion");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"INSERT INTO public.users (id, email, username, first_name, last_name, password_hash, status, created_at_utc) VALUES ('{Guid.NewGuid()}', 'suspended-{database}@example.test', 'suspended-{database}', 'Suspended', 'User', 'hash', 'Suspended', NOW())");

            await context.Database.MigrateAsync();
            (await ScalarAsync(connection, "SELECT COUNT(*) FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260922021122_CutoverUserAssignmentScopeVocabulary'")).ShouldBe(1);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task DottedPermissionCutover_WithUnknownPermission_ShouldFailBeforeCleanupOrHistory()
    {
        string database = $"migration_2a_guard_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        var permissionId = Guid.NewGuid();
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync("20260922012720_AddUserRoleScopeRowVersion");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            // This test migrates from the schema that predates the cutover, where permissions has no
            // name_ar column yet, so the insert must use the pre-cutover column list.
            await ExecuteAsync(connection, $"INSERT INTO public.permissions (id, code, description) VALUES ('{permissionId}', 'unknown.permission', 'test-only unknown permission')");

            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync());
            (await ScalarAsync(connection, $"SELECT COUNT(*) FROM public.permissions WHERE id = '{permissionId}'")).ShouldBe(1);
            (await ScalarAsync(connection, "SELECT COUNT(*) FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260922220000_CutoverToDottedOnlyPermissionVocabulary'")).ShouldBe(0);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task DottedPermissionCutover_WithMissingRoleGrantParity_ShouldFailBeforeCleanupOrHistory()
    {
        string database = $"migration_2a_role_guard_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        var roleId = Guid.NewGuid();
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync("20260922012720_AddUserRoleScopeRowVersion");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"INSERT INTO public.roles (id, name, description, created_at_utc) VALUES ('{roleId}', '2A role {roleId:N}', 'test-only', NOW())");
            await ExecuteAsync(connection, $"INSERT INTO public.role_permissions (permission_id, role_id) VALUES ('00000000-0000-0000-0000-000000000116', '{roleId}')");

            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync());
            (await ScalarAsync(connection, $"SELECT COUNT(*) FROM public.role_permissions WHERE role_id = '{roleId}' AND permission_id = '00000000-0000-0000-0000-000000000116'")).ShouldBe(1);
            (await ScalarAsync(connection, "SELECT COUNT(*) FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260922220000_CutoverToDottedOnlyPermissionVocabulary'")).ShouldBe(0);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task DottedPermissionCutover_WithMissingScopeParity_ShouldFailBeforeCleanupOrHistory()
    {
        string database = $"migration_2a_scope_guard_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync("20260922012720_AddUserRoleScopeRowVersion");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, "DELETE FROM public.permission_allowed_scope_types WHERE permission_id = '00000000-0000-0000-0000-000000000214' AND scope_type = 'Warehouse'");

            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync());
            long scopeCount = (long)(await ScalarAsync(connection, "SELECT COUNT(*) FROM public.permission_allowed_scope_types pas JOIN public.permissions p ON p.id = pas.permission_id WHERE p.code = 'warehouse-documents:create'") ?? 0L);
            scopeCount.ShouldBeGreaterThan(0);
            (await ScalarAsync(connection, "SELECT COUNT(*) FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260922220000_CutoverToDottedOnlyPermissionVocabulary'")).ShouldBe(0);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

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

    private ApplicationDbContext CreateContext(string connectionString)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new ApplicationDbContext(options, factory.Services.GetRequiredService<IDomainEventsDispatcher>());
    }

    private async Task<string> CreateDatabaseAsync(string database)
    {
        NpgsqlConnectionStringBuilder builder = new(factory.DatabaseConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"CREATE DATABASE \"{database}\"");
        builder.Database = database;
        return builder.ConnectionString;
    }

    private async Task DropDatabaseAsync(string database)
    {
        NpgsqlConnectionStringBuilder builder = new(factory.DatabaseConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is fixed at each call site and only uses Guid-generated identifiers in disposable Testcontainers databases.")]
    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

}
