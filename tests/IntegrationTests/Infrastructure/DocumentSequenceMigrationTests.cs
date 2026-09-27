using System.Diagnostics.CodeAnalysis;
using Domain.Common;
using Domain.DocumentSequences;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class DocumentSequenceMigrationTests(IntegrationTestWebAppFactory factory)
{
    private const string PreviousMigrationId = "20260927031550_LinkReceivingInfoToExternalSupplier";
    private const string MigrationId = "20260927033853_UnifyDocumentSequencesBySiteYear";
    private const string IdentityMigrationId = "20260927040753_AddDocumentReferenceIdentity";

    [Fact]
    public async Task Migration_ShouldConsolidateOldPerTypeCountersToMaximumAndPreserveReferences()
    {
        string database = $"migration_4d_sequence_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync(PreviousMigrationId);

            string suffix = Guid.NewGuid().ToString("N")[..8];
            var organizationId = Guid.NewGuid();
            var siteId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var receivingId = Guid.NewGuid();
            var issueId = Guid.NewGuid();
            string receivingReference = $"4DS-RCV-2026-000004-{suffix}";
            string issueReference = $"4DS-ISS-2026-000009-{suffix}";

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection,
                "INSERT INTO public.organizations (id,name,code,status,created_at_utc) VALUES (@id,@name,@code,'Active',NOW())",
                ("id", organizationId), ("name", $"4D Org {suffix}"), ("code", $"4DO{suffix}"));
            await ExecuteAsync(connection,
                "INSERT INTO public.sites (id,organization_id,name,code,status,created_at_utc) VALUES (@id,@organization,@name,@code,'Active',NOW())",
                ("id", siteId), ("organization", organizationId), ("name", $"4D Site {suffix}"), ("code", $"4DS{suffix}"));
            await ExecuteAsync(connection,
                "INSERT INTO public.warehouses (id,site_id,name,code,warehouse_type,can_hold_stock,status,row_version,created_at_utc) " +
                "VALUES (@id,@site,@name,@code,'Main',TRUE,'Active',1,NOW())",
                ("id", warehouseId), ("site", siteId), ("name", $"4D Warehouse {suffix}"), ("code", $"4DW{suffix}"));
            await ExecuteAsync(connection,
                "INSERT INTO public.warehouse_documents (id,warehouse_id,document_type,system_reference_number,document_status,row_version,created_at_utc) " +
                "VALUES (@id,@warehouse,'Receiving',@reference,'Draft',1,NOW()),(@issue,@warehouse,'Issue',@issue_reference,'Draft',1,NOW())",
                ("id", receivingId), ("warehouse", warehouseId), ("reference", receivingReference),
                ("issue", issueId), ("issue_reference", issueReference));
            foreach ((string type, int lastSequence) in new[]
            {
                ("Receiving", 4), ("Issue", 9), ("Transfer", 6)
            })
            {
                await ExecuteAsync(connection,
                    "INSERT INTO public.document_sequences " +
                    "(id, site_id, document_type, year, last_sequence, created_at_utc) " +
                    "VALUES (@id, @site, @type, 2026, @last, NOW())",
                    ("id", Guid.NewGuid()), ("site", siteId), ("type", type), ("last", lastSequence));
            }

            await context.Database.MigrateAsync();

            (await ScalarAsync(connection,
                "SELECT count(*) FROM public.document_sequences WHERE site_id = @site AND year = 2026",
                ("site", siteId))).ShouldBe(1L);
            (await ScalarAsync(connection,
                "SELECT last_sequence FROM public.document_sequences WHERE site_id = @site AND year = 2026",
                ("site", siteId))).ShouldBe(9);
            (await ColumnExistsAsync(connection, "document_sequences", "document_type")).ShouldBeFalse();
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeTrue();
            (await ScalarAsync(connection,
                "SELECT system_reference_number FROM public.warehouse_documents WHERE id = @id",
                ("id", receivingId))).ShouldBe(receivingReference);
            (await ScalarAsync(connection,
                "SELECT system_reference_number FROM public.warehouse_documents WHERE id = @id",
                ("id", issueId))).ShouldBe(issueReference);
            (await ScalarAsync(connection,
                "SELECT count(*) FROM public.warehouse_documents WHERE id IN (@receiving, @issue) " +
                "AND reference_site_id IS NULL AND reference_year IS NULL AND reference_sequence IS NULL",
                ("receiving", receivingId), ("issue", issueId))).ShouldBe(2L);

            await ExecuteAsync(connection,
                "UPDATE public.warehouse_documents SET reference_site_id=@site, reference_year=2026, reference_sequence=1 WHERE id=@id",
                ("site", siteId), ("id", receivingId));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(connection,
                "UPDATE public.warehouse_documents SET reference_site_id=@site, reference_year=2026, reference_sequence=1 WHERE id=@id",
                ("site", siteId), ("id", issueId)));
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(connection,
                "UPDATE public.warehouse_documents SET reference_sequence=NULL WHERE id=@id",
                ("id", receivingId)));

            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync(PreviousMigrationId));
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeTrue();
            (await MigrationAppliedAsync(connection, IdentityMigrationId)).ShouldBeTrue();
            (await ScalarAsync(connection,
                "SELECT last_sequence FROM public.document_sequences WHERE site_id = @site AND year = 2026",
                ("site", siteId))).ShouldBe(9);
            (await ScalarAsync(connection,
                "SELECT count(*) FROM public.warehouse_documents WHERE id = @id AND " +
                "reference_site_id = @site AND reference_year = 2026 AND reference_sequence = 1",
                ("id", receivingId), ("site", siteId))).ShouldBe(1L);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    private ApplicationDbContext CreateContext(string connectionString)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
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

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Test database identifiers are generated from GUIDs; row values are bound parameters.")]
    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 60 };
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQL text is fixed and only test-generated values are bound parameters.")]
    private static async Task<object?> ScalarAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 60 };
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteScalarAsync();
    }

    private static async Task<bool> ColumnExistsAsync(NpgsqlConnection connection, string table, string column) =>
        (bool)(await ScalarAsync(connection,
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name=@table AND column_name=@column)",
            ("table", table), ("column", column)))!;

    private static async Task<bool> MigrationAppliedAsync(NpgsqlConnection connection, string migrationId) =>
        (bool)(await ScalarAsync(connection,
            "SELECT EXISTS (SELECT 1 FROM public.\"__EFMigrationsHistory\" WHERE migration_id=@id)",
            ("id", migrationId)))!;
}
