using System.Diagnostics.CodeAnalysis;
using Domain.Common;
using Domain.Organizations;
using Domain.ReceivingInfos;
using Domain.Sites;
using Domain.TransferInfos;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ReceivingTypeMigrationSafetyTests(IntegrationTestWebAppFactory factory)
{
    private const string PreviousMigrationId = "20260927021037_RemoveMaterialFamilyBaseUnit";
    private const string MigrationId = "20260927025959_RestrictReceivingInfoToSupplier";

    [Fact]
    public async Task Migration_ShouldKeepSupplierAndEnforceSupplierOnly_OnFreshDatabase()
    {
        string database = $"migration_4b_supplier_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync(MigrationId);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeTrue();
            (await ScalarAsync(connection,
                "SELECT COUNT(*) FROM public.receiving_info_legacy_classifications")).ShouldBe(0L);
            string constraint = (string)(await ScalarAsync(connection,
                "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_receiving_info_receiving_type_valid'"))!;
            constraint.ShouldContain("Supplier");
            constraint.ShouldNotContain("Transfer");
            constraint.ShouldNotContain("Return");

            await context.Database.MigrateAsync(PreviousMigrationId);
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeFalse();
            (await TableExistsAsync(connection, "receiving_info_legacy_classifications")).ShouldBeFalse();
            string revertedConstraint = (string)(await ScalarAsync(connection,
                "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_receiving_info_receiving_type_valid'"))!;
            revertedConstraint.ShouldContain("Transfer");
            revertedConstraint.ShouldContain("Return");
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task Migration_ShouldArchiveOnlyAlreadyRepresentedTransfer_AndPreserveLegacyValues()
    {
        string database = $"migration_4b_transfer_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync(PreviousMigrationId);
            Guid documentId = await SeedTransferWithDetailAsync(context);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, "DROP TRIGGER trg_receiving_info_document_type ON public.receiving_info");
            await ExecuteAsync(connection,
                "INSERT INTO public.receiving_info (document_id, supplier_ref, supplier_invoice_ref, receiving_type, created_at_utc) " +
                "VALUES (@id, 'Legacy transfer label', 'legacy transfer invoice', 'Transfer', NOW())",
                ("id", documentId));

            await context.Database.MigrateAsync(MigrationId);

            (await ScalarAsync(connection,
                "SELECT COUNT(*) FROM public.receiving_info WHERE document_id = @id", ("id", documentId))).ShouldBe(0L);
            (await ScalarAsync(connection,
                "SELECT receiving_type FROM public.receiving_info_legacy_classifications WHERE document_id = @id",
                ("id", documentId))).ShouldBe("Transfer");
            (await ScalarAsync(connection,
                "SELECT supplier_ref FROM public.receiving_info_legacy_classifications WHERE document_id = @id",
                ("id", documentId))).ShouldBe("Legacy transfer label");
            (await ScalarAsync(connection,
                "SELECT supplier_invoice_ref FROM public.receiving_info_legacy_classifications WHERE document_id = @id",
                ("id", documentId))).ShouldBe("legacy transfer invoice");
            (await ScalarAsync(connection,
                "SELECT classification FROM public.receiving_info_legacy_classifications WHERE document_id = @id",
                ("id", documentId))).ShouldBe("AlreadyRepresentedByTransferInfo");
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeTrue();

            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(connection,
                "INSERT INTO public.receiving_info (document_id, supplier_ref, receiving_type, created_at_utc) " +
                "VALUES (@id, 'not a supplier', 'Transfer', NOW())", ("id", documentId)));
            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync(PreviousMigrationId));
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeTrue();
            (await ScalarAsync(connection,
                "SELECT COUNT(*) FROM public.receiving_info_legacy_classifications WHERE document_id = @id",
                ("id", documentId))).ShouldBe(1L);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task Migration_ShouldFailClosedAndPreserveLegacyRow_WhenTransferIsNotMapped()
    {
        string database = $"migration_4b_ambiguous_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync(PreviousMigrationId);
            Guid documentId = await SeedReceivingDocumentAsync(context);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection,
                "INSERT INTO public.receiving_info (document_id, supplier_ref, supplier_invoice_ref, receiving_type, created_at_utc) " +
                "VALUES (@id, 'free form destination label', 'legacy invoice', 'Transfer', NOW())",
                ("id", documentId));

            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync(MigrationId));

            (await ScalarAsync(connection,
                "SELECT receiving_type FROM public.receiving_info WHERE document_id = @id", ("id", documentId)))
                .ShouldBe("Transfer");
            (await TableExistsAsync(connection, "receiving_info_legacy_classifications")).ShouldBeFalse();
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeFalse();
            string legacyConstraint = (string)(await ScalarAsync(connection,
                "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_receiving_info_receiving_type_valid'"))!;
            legacyConstraint.ShouldContain("Transfer");
            legacyConstraint.ShouldContain("Return");
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    private static async Task<Guid> SeedReceivingDocumentAsync(ApplicationDbContext context)
    {
        Guid warehouseId = await SeedWarehouseAsync(context);
        var documentId = Guid.NewGuid();
        await InsertLegacyDocumentAsync(context, documentId, warehouseId, "Receiving", $"RCV-{Guid.NewGuid():N}");
        return documentId;
    }

    private static async Task<Guid> SeedTransferWithDetailAsync(ApplicationDbContext context)
    {
        Guid warehouseId = await SeedWarehouseAsync(context);
        Guid destinationWarehouseId = await SeedWarehouseAsync(context);
        var documentId = Guid.NewGuid();
        await InsertLegacyDocumentAsync(context, documentId, warehouseId, "Transfer", $"TRF-{Guid.NewGuid():N}");
        context.Add(TransferInfo.Create(documentId, destinationWarehouseId, "Already mapped transfer").Value);
        await context.SaveChangesAsync();
        return documentId;
    }

    private static async Task InsertLegacyDocumentAsync(
        ApplicationDbContext context,
        Guid documentId,
        Guid warehouseId,
        string documentType,
        string reference)
    {
        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();
        await ExecuteAsync(connection,
            "INSERT INTO public.warehouse_documents " +
            "(id, warehouse_id, document_type, system_reference_number, document_status, row_version, created_at_utc) " +
            "VALUES (@id, @warehouse, @type, @reference, 'Draft', 1, NOW())",
            ("id", documentId), ("warehouse", warehouseId), ("type", documentType), ("reference", reference));
    }

    private static async Task<Guid> SeedWarehouseAsync(ApplicationDbContext context)
    {
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var organization = Organization.Create(Guid.NewGuid(), $"Receiving migration org {suffix}", $"RMO{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Receiving migration site {suffix}", $"RMS{suffix}", null);
        var warehouse = Warehouse.Create(Guid.NewGuid(), site.Id, $"Receiving migration warehouse {suffix}", $"RMW{suffix}", "Test", true);
        context.AddRange(organization, site, warehouse);
        await context.SaveChangesAsync();
        return warehouse.Id;
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
        Justification = "Test database identifiers are generated from GUIDs; all row values are bound parameters.")]
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

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string table) =>
        (bool)(await ScalarAsync(connection,
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='public' AND table_name=@table)",
            ("table", table)))!;

    private static async Task<bool> MigrationAppliedAsync(NpgsqlConnection connection, string migrationId) =>
        (bool)(await ScalarAsync(connection,
            "SELECT EXISTS (SELECT 1 FROM public.\"__EFMigrationsHistory\" WHERE migration_id=@id)",
            ("id", migrationId)))!;
}
