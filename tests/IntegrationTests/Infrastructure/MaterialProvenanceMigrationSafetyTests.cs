using System.Diagnostics.CodeAnalysis;
using Domain.DocumentLines;
using SharedKernel;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.Organizations;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// 3B migration gate. <c>AddMaterialConversionProvenance</c> is the only schema change that turns the
/// material classification into a derived, versioned fact and that stores the document-line
/// provenance snapshot, so it is verified on a disposable database: the new shape, the guard that
/// refuses to drop a column whose stored value contradicts the derived rule, and the rollback that
/// restores the dropped column from that same rule.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class MaterialProvenanceMigrationSafetyTests(IntegrationTestWebAppFactory factory)
{
    private const string FoundationalMigrationId = "20260926163309_AddMaterialConversionProvenance";
    private const string PreviousMigrationId = "20260922220000_CutoverToDottedOnlyPermissionVocabulary";

    [Fact]
    public async Task ProvenanceMigration_OnFreshDatabase_ShouldAddProvenanceShapeAndDeriveTheAssetFlag()
    {
        string database = $"migration_3b_shape_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync(FoundationalMigrationId);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // The derived rule replaces the stored flag.
            (await ColumnExistsAsync(connection, "materials", "requires_asset_number")).ShouldBeFalse();
            (await ColumnExistsAsync(connection, "materials", "catalog_version")).ShouldBeTrue();
            (await ColumnInfoAsync(connection, "materials", "catalog_version")).ShouldBe(("NO", "1"));

            foreach (string column in new[]
            {
                "source_material_version", "source_material_kind", "source_tracking_type", "source_base_unit_id",
                "source_conversion_id", "source_conversion_from_unit_id", "source_conversion_to_unit_id",
                "source_conversion_factor"
            })
            {
                (await ColumnExistsAsync(connection, "document_lines", column))
                    .ShouldBeTrue($"document_lines.{column} is part of the 3B provenance snapshot");
            }

            (await ColumnInfoAsync(connection, "document_lines", "source_conversion_factor"))
                .ShouldBe(("YES", null));
            (await ConstraintExistsAsync(connection, "document_lines", "ck_document_lines_source_conversion_complete"))
                .ShouldBeTrue();
            (await ConstraintExistsAsync(connection, "document_lines", "ck_document_lines_source_conversion_required"))
                .ShouldBeTrue();
            (await ConstraintExistsAsync(connection, "document_lines", "ck_document_lines_source_material_version_positive"))
                .ShouldBeTrue();
            (await ConstraintExistsAsync(connection, "materials", "ck_materials_catalog_version_positive"))
                .ShouldBeTrue();
            (await IndexExistsAsync(connection, "ix_document_lines_source_conversion_id")).ShouldBeTrue();
            (await IndexExistsAsync(connection, "ix_document_lines_document_id_source_material_version"))
                .ShouldBeTrue();
            (await MigrationAppliedAsync(connection, FoundationalMigrationId)).ShouldBeTrue();

            Guid materialId = await SeedConsumableMaterialAsync(context);
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                connection,
                "UPDATE public.materials SET catalog_version = 0 WHERE id = @id",
                ("id", materialId)));
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task ProvenanceMigration_WithContradictoryStoredAssetFlag_ShouldFailBeforeDroppingTheColumn()
    {
        string database = $"migration_3b_guard_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync(FoundationalMigrationId);
            Guid materialId = await SeedConsumableMaterialAsync(context);

            // Reconstruct only the pre-3B schema in this disposable database. Do not migrate back
            // from the current head: later 4D migrations intentionally have no safe Down path.
            await ReconstructPreProvenanceSchemaAsync(context);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            (await ColumnExistsAsync(connection, "materials", "requires_asset_number")).ShouldBeTrue();
            await ExecuteAsync(
                connection,
                "UPDATE public.materials SET requires_asset_number = true WHERE id = @id",
                ("id", materialId));

            // Act
            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync(FoundationalMigrationId));

            // Assert
            (await ColumnExistsAsync(connection, "materials", "requires_asset_number"))
                .ShouldBeTrue("the guard must refuse before the column is dropped");
            (await ScalarAsync(
                connection,
                "SELECT requires_asset_number FROM public.materials WHERE id = @id",
                ("id", materialId))).ShouldBe(true);
            (await MigrationAppliedAsync(connection, FoundationalMigrationId)).ShouldBeFalse();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task ProvenanceMigration_Rollback_ShouldRestoreTheAssetFlagFromTheDerivedRuleAndDropTheProvenanceShape()
    {
        string database = $"migration_3b_down_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync(FoundationalMigrationId);
            Guid consumableId = await SeedConsumableMaterialAsync(context);
            Guid assetId = await SeedAssetMaterialAsync(context);

            // Act
            await context.Database.MigrateAsync(PreviousMigrationId);

            // Assert
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            (await ScalarAsync(
                connection,
                "SELECT requires_asset_number FROM public.materials WHERE id = @id",
                ("id", consumableId))).ShouldBe(false);
            (await ScalarAsync(
                connection,
                "SELECT requires_asset_number FROM public.materials WHERE id = @id",
                ("id", assetId))).ShouldBe(true);
            (await ColumnExistsAsync(connection, "materials", "catalog_version")).ShouldBeFalse();
            (await ColumnExistsAsync(connection, "document_lines", "source_material_version")).ShouldBeFalse();
            (await ColumnExistsAsync(connection, "document_lines", "source_conversion_id")).ShouldBeFalse();
            (await ConstraintExistsAsync(connection, "document_lines", "ck_document_lines_source_conversion_complete"))
                .ShouldBeFalse();
            (await IndexExistsAsync(connection, "ix_document_lines_source_conversion_id")).ShouldBeFalse();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task FoundationalMigration_ShouldAddColumnsIndexesAndAllProvenanceConstraints()
    {
        string database = $"migration_3b_foundational_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync(FoundationalMigrationId);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            (await ColumnExistsAsync(connection, "materials", "catalog_version")).ShouldBeTrue();
            (await ColumnInfoAsync(connection, "materials", "catalog_version")).ShouldBe(("NO", "1"));
            (await ColumnExistsAsync(connection, "materials", "requires_asset_number")).ShouldBeFalse();

            foreach (string column in new[]
            {
                "source_material_version", "source_material_kind", "source_tracking_type", "source_base_unit_id",
                "source_conversion_id", "source_conversion_from_unit_id", "source_conversion_to_unit_id",
                "source_conversion_factor"
            })
            {
                (await ColumnExistsAsync(connection, "document_lines", column))
                    .ShouldBeTrue($"document_lines.{column} is part of the 3B provenance snapshot");
            }

            (await ConstraintExistsAsync(connection, "document_lines", "ck_document_lines_source_conversion_complete"))
                .ShouldBeTrue();
            (await ConstraintExistsAsync(connection, "document_lines", "ck_document_lines_source_conversion_required"))
                .ShouldBeTrue();
            (await ConstraintExistsAsync(connection, "document_lines", "ck_document_lines_source_material_version_positive"))
                .ShouldBeTrue();
            (await IndexExistsAsync(connection, "ix_document_lines_source_conversion_id")).ShouldBeTrue();
            (await IndexExistsAsync(connection, "ix_document_lines_document_id_source_material_version"))
                .ShouldBeTrue();

            (await ConstraintExistsAsync(connection, "materials", "ck_materials_catalog_version_positive"))
                .ShouldBeTrue();
            (await MigrationAppliedAsync(connection, FoundationalMigrationId)).ShouldBeTrue();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task FoundationalMigration_Rollback_ShouldRestorePreProvenanceSchema()
    {
        string database = $"migration_3b_foundational_down_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync(FoundationalMigrationId);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            (await ColumnExistsAsync(connection, "materials", "catalog_version")).ShouldBeTrue();
            (await ColumnExistsAsync(connection, "document_lines", "source_material_version")).ShouldBeTrue();

            await context.Database.MigrateAsync(PreviousMigrationId);

            (await ColumnExistsAsync(connection, "materials", "catalog_version")).ShouldBeFalse();
            (await ColumnExistsAsync(connection, "document_lines", "source_material_version")).ShouldBeFalse();
            (await ConstraintExistsAsync(connection, "document_lines", "ck_document_lines_source_conversion_complete"))
                .ShouldBeFalse();
            (await IndexExistsAsync(connection, "ix_document_lines_source_conversion_id")).ShouldBeFalse();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task ProvenanceConstraint_ShouldRejectPartialCatalogSnapshot()
    {
        string database = $"migration_3b_partial_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();
            (Guid lineId, _) = await SeedCatalogDocumentLineAsync(context, Guid.NewGuid());
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // A revision without classification is not a usable provenance snapshot.
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                connection,
                "UPDATE public.document_lines SET source_material_kind = NULL WHERE id = @id",
                ("id", lineId)));
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The relation name is a fixed literal and the only value parameter is a Guid generated inside a disposable Testcontainers database.")]
    public async Task ConvertedUnitLine_WithoutCapturedConversion_ShouldBeRejectedByTheProvenanceConstraint()
    {
        string database = $"migration_3b_constraint_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();
            var sourceUnitId = Guid.NewGuid();
            (Guid lineId, Guid baseUnitId) = await SeedCatalogDocumentLineAsync(context, sourceUnitId);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Act
            await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
                connection,
                "UPDATE public.document_lines SET unit_id = @unit_id, source_conversion_id = NULL, " +
                "source_conversion_from_unit_id = NULL, source_conversion_to_unit_id = NULL, " +
                "source_conversion_factor = NULL WHERE id = @id",
                ("unit_id", sourceUnitId),
                ("id", lineId)));

            // Assert
            (await ScalarAsync(
                connection,
                "SELECT unit_id FROM public.document_lines WHERE id = @id",
                ("id", lineId))).ShouldBe(baseUnitId);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    private async Task<Guid> SeedConsumableMaterialAsync(ApplicationDbContext context) =>
        await SeedMaterialAsync(context, MaterialKind.Consumable, TrackingType.Quantity);

    private async Task<Guid> SeedAssetMaterialAsync(ApplicationDbContext context) =>
        await SeedMaterialAsync(context, MaterialKind.Asset, TrackingType.Serial);

    private async Task<Guid> SeedMaterialAsync(
        ApplicationDbContext context,
        MaterialKind materialKind,
        TrackingType trackingType)
    {
        CatalogChain chain = await SeedCatalogChainAsync(context);
        var materialId = Guid.NewGuid();
        context.Materials.Add(Material.Create(
            materialId, chain.FamilyId, chain.BaseUnitId, "مادة", "Material",
            $"MAT-{materialId:N}", materialKind, trackingType, false, null));
        await context.SaveChangesAsync();
        return materialId;
    }

    private async Task<CatalogChain> SeedCatalogChainAsync(ApplicationDbContext context)
    {
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();

        context.MaterialDomains.Add(MaterialDomain.Create(domainId, $"Domain {suffix}", $"D{suffix}"));
        context.MaterialCategories.Add(MaterialCategory.Create(
            categoryId, domainId, null, $"Category {suffix}", $"C{suffix}"));

        // The test targets 3B, before AddUnitOfMeasureStatus. Use the historical UOM row shape
        // when status is absent rather than letting today's EF model write against that schema.
        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();
        bool hasUnitStatus = await ColumnExistsAsync(connection, "units_of_measure", "status");
        if (hasUnitStatus)
        {
            context.UnitsOfMeasure.Add(UnitOfMeasure.Create(baseUnitId, "Piece", "pc", "Count"));
        }
        await context.SaveChangesAsync();

        if (!hasUnitStatus)
        {
            await ExecuteAsync(connection,
                "INSERT INTO public.units_of_measure (id, name, symbol, unit_type, created_at_utc) " +
                "VALUES (@id, 'Piece', 'pc', 'Count', NOW())",
                ("id", baseUnitId));
        }

        // The current EF model no longer has MaterialFamily.BaseUnitId, while this historical
        // migration test intentionally exercises the schema before 3C removes that column.
        bool hasFamilyBaseUnit = await ColumnExistsAsync(connection, "material_families", "base_unit_id");
        string insertFamilySql = hasFamilyBaseUnit
            ? "INSERT INTO public.material_families (id, category_id, name, code, base_unit_id, status, created_at_utc) " +
              "VALUES (@id, @category, @name, @code, @unit, 'Active', NOW())"
            : "INSERT INTO public.material_families (id, category_id, name, code, status, created_at_utc) " +
              "VALUES (@id, @category, @name, @code, 'Active', NOW())";
        if (hasFamilyBaseUnit)
        {
            await ExecuteAsync(connection, insertFamilySql,
                ("id", familyId), ("category", categoryId), ("name", $"Family {suffix}"),
                ("code", $"F{suffix}"), ("unit", baseUnitId));
        }
        else
        {
            await ExecuteAsync(connection, insertFamilySql,
                ("id", familyId), ("category", categoryId), ("name", $"Family {suffix}"),
                ("code", $"F{suffix}"));
        }

        return new CatalogChain(familyId, baseUnitId);
    }

    private async Task<(Guid LineId, Guid BaseUnitId)> SeedCatalogDocumentLineAsync(
        ApplicationDbContext context,
        Guid sourceUnitId)
    {
        string suffix = Guid.NewGuid().ToString("N")[..12];
        CatalogChain chain = await SeedCatalogChainAsync(context);
        Guid baseUnitId = chain.BaseUnitId;

        var organization = Organization.Create(
            Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var warehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"W{suffix}", "General", true);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouse.Id, Domain.Common.DocumentType.Receiving, $"REC3B-{suffix}");
        document.UpdatePaperReference($"P-{suffix}", 2026);

        var materialId = Guid.NewGuid();
        context.AddRange(organization, site, warehouse);
        context.WarehouseDocuments.Add(document);
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(sourceUnitId, "Box", "box", "Count"));
        context.Materials.Add(Material.Create(
            materialId, chain.FamilyId, baseUnitId, "مادة", "Material", $"MAT-{materialId:N}",
            MaterialKind.Consumable, TrackingType.Quantity, false, null));
        await context.SaveChangesAsync();

        Result<DocumentLine> line = DocumentLine.Create(
            Guid.NewGuid(), document.Id, materialId, Domain.Common.DocumentLineType.Normal,
            1m, baseUnitId, 1m, null, null, null,
            provenance: new DocumentLineProvenance(
                1,
                MaterialKind.Consumable,
                TrackingType.Quantity,
                baseUnitId));
        context.DocumentLines.Add(line.Value);
        await context.SaveChangesAsync();

        return (line.Value.Id, baseUnitId);
    }

    private sealed record CatalogChain(Guid FamilyId, Guid BaseUnitId);

    private ApplicationDbContext CreateContext(string connectionString)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(
                typeof(ApplicationDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new ApplicationDbContext(options, factory.Services.GetRequiredService<IDomainEventsDispatcher>());
    }

    /// <summary>Reconstructs the legacy 3B schema in a disposable test database only.</summary>
    private async Task ReconstructPreProvenanceSchemaAsync(ApplicationDbContext context)
    {
        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();
        await ExecuteAsync(connection,
            "ALTER TABLE public.materials ADD COLUMN requires_asset_number boolean NOT NULL DEFAULT false; " +
            "UPDATE public.materials SET requires_asset_number = (material_kind = 'Asset'); " +
            "ALTER TABLE public.materials DROP CONSTRAINT ck_materials_catalog_version_positive; " +
            "ALTER TABLE public.materials DROP COLUMN catalog_version; " +
            "ALTER TABLE public.document_lines DROP CONSTRAINT ck_document_lines_source_provenance_complete; " +
            "ALTER TABLE public.document_lines DROP CONSTRAINT ck_document_lines_source_material_version_positive; " +
            "ALTER TABLE public.document_lines DROP CONSTRAINT ck_document_lines_source_conversion_required; " +
            "ALTER TABLE public.document_lines DROP CONSTRAINT ck_document_lines_source_conversion_complete; " +
            "ALTER TABLE public.document_lines DROP COLUMN source_material_version, " +
            "DROP COLUMN source_material_kind, DROP COLUMN source_tracking_type, " +
            "DROP COLUMN source_base_unit_id, DROP COLUMN source_conversion_id, " +
            "DROP COLUMN source_conversion_from_unit_id, DROP COLUMN source_conversion_to_unit_id, " +
            "DROP COLUMN source_conversion_factor; " +
            "DELETE FROM public.\"__EFMigrationsHistory\" WHERE migration_id = @id",
            ("id", FoundationalMigrationId));
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The relation names are fixed literals, and the only value parameters are Guids generated inside a disposable Testcontainers database.")]
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is fixed at each call site and the only value parameters are Guids generated inside a disposable Testcontainers database.")]
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table and column names are fixed literals supplied by the test, not by request input.")]
    private static async Task<bool> ColumnExistsAsync(NpgsqlConnection connection, string table, string column) =>
        (bool)(await ScalarAsync(
            connection,
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' " +
            "AND table_name = @table AND column_name = @column)",
            ("table", table),
            ("column", column)))!;

    /// <summary>Returns the nullability and default of a column, or (null, null) when it is absent.</summary>
    private static async Task<(string? IsNullable, string? ColumnDefault)> ColumnInfoAsync(
        NpgsqlConnection connection,
        string table,
        string column)
    {
        await using var command = new NpgsqlCommand(
            "SELECT is_nullable, column_default FROM information_schema.columns " +
            "WHERE table_schema = 'public' AND table_name = @table AND column_name = @column",
            connection);
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("column", column);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return (null, null);
        }

        return (reader.GetString(0), await reader.IsDBNullAsync(1) ? null : reader.GetString(1));
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table and constraint names are fixed literals supplied by the test, not by request input.")]
    private static async Task<bool> ConstraintExistsAsync(NpgsqlConnection connection, string table, string constraint) =>
        (bool)(await ScalarAsync(
            connection,
            "SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_constraint c " +
            "INNER JOIN pg_catalog.pg_class t ON t.oid = c.conrelid " +
            "INNER JOIN pg_catalog.pg_namespace n ON n.oid = t.relnamespace " +
            "WHERE n.nspname = 'public' AND t.relname = @table AND c.conname = @constraint)",
            ("table", table),
            ("constraint", constraint)))!;

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The index name is a fixed literal supplied by the test, not by request input.")]
    private static async Task<bool> IndexExistsAsync(NpgsqlConnection connection, string index) =>
        (bool)(await ScalarAsync(
            connection,
            "SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_indexes WHERE schemaname = 'public' " +
            "AND indexname = @index)",
            ("index", index)))!;

    /// <summary>
    /// The history table is created with the context's naming convention, so the snake_case fixture
    /// reads <c>migration_id</c> rather than the default <c>MigrationId</c> column.
    /// </summary>
    private static async Task<bool> MigrationAppliedAsync(NpgsqlConnection connection, string migrationId) =>
        (bool)(await ScalarAsync(
            connection,
            "SELECT EXISTS (SELECT 1 FROM public.\"__EFMigrationsHistory\" WHERE migration_id = @id)",
            ("id", migrationId)))!;
}
