using System.Diagnostics.CodeAnalysis;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.MaterialUnitConversions;
using Domain.Materials;
using Domain.UnitsOfMeasure;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class MaterialFamilyBaseUnitMigrationTests(IntegrationTestWebAppFactory factory)
{
    private const string MigrationId = "20260927021037_RemoveMaterialFamilyBaseUnit";

    [Fact]
    public async Task Migration_ShouldDropOnlyFamilyUnitAndPreserveMaterialUnit()
    {
        string database = $"migration_3c_shape_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            (await ColumnExistsAsync(connection, "material_families", "base_unit_id")).ShouldBeFalse();
            (await ColumnExistsAsync(connection, "materials", "base_unit_id")).ShouldBeTrue();
            (await ConstraintExistsAsync(connection, "materials", "fk_materials_units_of_measure_base_unit_id"))
                .ShouldBeTrue();
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeTrue();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task Migration_ShouldFailClosedWhenConversionTargetDiffersFromMaterialBaseUnit()
    {
        string database = $"migration_3c_guard_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();
            (Guid materialId, Guid familyId, Guid baseUnitId, Guid sourceUnitId, Guid alternateUnitId) =
                await SeedMaterialAndUnitsAsync(context);
            context.MaterialUnitConversions.Add(MaterialUnitConversion.Create(
                Guid.NewGuid(), materialId, sourceUnitId, baseUnitId, 12m));
            await context.SaveChangesAsync();
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await RestoreLegacyFamilyColumnForReplayAsync(connection, familyId, baseUnitId);
            await ExecuteAsync(
                connection,
                "UPDATE public.material_unit_conversions SET to_base_unit_id = @alternate WHERE material_id = @material",
                ("alternate", alternateUnitId),
                ("material", materialId));

            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync());

            (await ColumnExistsAsync(connection, "material_families", "base_unit_id"))
                .ShouldBeTrue("the guard must run before dropping the legacy family column");
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeFalse();
            (await ScalarAsync(connection,
                "SELECT base_unit_id FROM public.materials WHERE id = @id", ("id", materialId)))
                .ShouldBe(baseUnitId);
            (await ScalarAsync(connection,
                "SELECT family_id FROM public.materials WHERE id = @id", ("id", materialId)))
                .ShouldBe(familyId);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task Migration_ShouldFailClosedWhenMaterialBaseUnitIsOrphaned()
    {
        string database = $"migration_3c_orphan_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();
            (Guid MaterialId, Guid FamilyId, Guid BaseUnitId, Guid SourceUnitId, Guid AlternateUnitId) seeded =
                await SeedMaterialAndUnitsAsync(context);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await RestoreLegacyFamilyColumnForReplayAsync(connection, seeded.FamilyId, seeded.BaseUnitId);
            await ExecuteAsync(connection,
                "ALTER TABLE public.materials DROP CONSTRAINT fk_materials_units_of_measure_base_unit_id");
            var orphanedUnitId = Guid.NewGuid();
            await ExecuteAsync(connection,
                "UPDATE public.materials SET base_unit_id = @orphan WHERE id = @id",
                ("orphan", orphanedUnitId), ("id", seeded.MaterialId));

            await Should.ThrowAsync<PostgresException>(() => context.Database.MigrateAsync());

            (await ColumnExistsAsync(connection, "material_families", "base_unit_id")).ShouldBeTrue();
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeFalse();
            (await ScalarAsync(connection,
                "SELECT base_unit_id FROM public.materials WHERE id = @id", ("id", seeded.MaterialId)))
                .ShouldBe(orphanedUnitId);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task Migration_Down_ShouldRefuseToInventFormerFamilyUnit()
    {
        string database = $"migration_3c_down_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            IMigrationsAssembly migrations = context.Database.GetService<IMigrationsAssembly>();
            Migration migration = migrations.CreateMigration(
                migrations.Migrations[MigrationId],
                context.Database.ProviderName!);
            await Should.ThrowAsync<NotSupportedException>(
                () => Task.Run(() => _ = migration.DownOperations));

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            (await ColumnExistsAsync(connection, "material_families", "base_unit_id")).ShouldBeFalse();
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeTrue();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    private async Task<(Guid MaterialId, Guid FamilyId, Guid BaseUnitId, Guid SourceUnitId, Guid AlternateUnitId)>
        SeedMaterialAndUnitsAsync(ApplicationDbContext context)
    {
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();
        var sourceUnitId = Guid.NewGuid();
        var alternateUnitId = Guid.NewGuid();

        context.MaterialDomains.Add(MaterialDomain.Create(domainId, $"Domain {suffix}", $"D{suffix}"));
        context.MaterialCategories.Add(MaterialCategory.Create(
            categoryId, domainId, null, $"Category {suffix}", $"C{suffix}"));
        context.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, $"Family {suffix}", $"F{suffix}"));
        context.UnitsOfMeasure.AddRange(
            UnitOfMeasure.Create(baseUnitId, "Piece", "pc", "Count"),
            UnitOfMeasure.Create(sourceUnitId, "Box", "box", "Count"),
            UnitOfMeasure.Create(alternateUnitId, "Pack", "pack", "Count"));
        context.Materials.Add(Material.Create(
            materialId, familyId, baseUnitId, "مادة", "Material", $"M{suffix}",
            MaterialKind.Consumable, TrackingType.Quantity, false, null));
        await context.SaveChangesAsync();

        return (materialId, familyId, baseUnitId, sourceUnitId, alternateUnitId);
    }

    private static async Task RestoreLegacyFamilyColumnForReplayAsync(
        NpgsqlConnection connection,
        Guid familyId,
        Guid baseUnitId)
    {
        await ExecuteAsync(connection,
            "ALTER TABLE public.material_families ADD COLUMN base_unit_id uuid");
        await ExecuteAsync(connection,
            "UPDATE public.material_families SET base_unit_id = @unit WHERE id = @family",
            ("unit", baseUnitId), ("family", familyId));
        await ExecuteAsync(connection,
            "ALTER TABLE public.material_families ALTER COLUMN base_unit_id SET NOT NULL");
        await ExecuteAsync(connection,
            "CREATE INDEX ix_material_families_base_unit_id ON public.material_families (base_unit_id)");
        await ExecuteAsync(connection,
            "ALTER TABLE public.material_families ADD CONSTRAINT fk_material_families_units_of_measure_base_unit_id " +
            "FOREIGN KEY (base_unit_id) REFERENCES public.units_of_measure (id) ON DELETE CASCADE");
        await ExecuteAsync(connection,
            "DELETE FROM public.\"__EFMigrationsHistory\" WHERE migration_id = @id",
            ("id", MigrationId));
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
        Justification = "SQL text is fixed at each call site; dynamically interpolated database identifiers are generated GUID-based names in an isolated Testcontainers instance.")]
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
        Justification = "SQL text is fixed and only test-generated parameters are bound.")]
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

    private static async Task<bool> ConstraintExistsAsync(NpgsqlConnection connection, string table, string constraint) =>
        (bool)(await ScalarAsync(connection,
            "SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_constraint c JOIN pg_catalog.pg_class t ON t.oid=c.conrelid JOIN pg_catalog.pg_namespace n ON n.oid=t.relnamespace WHERE n.nspname='public' AND t.relname=@table AND c.conname=@constraint)",
            ("table", table), ("constraint", constraint)))!;

    private static async Task<bool> MigrationAppliedAsync(NpgsqlConnection connection, string migrationId) =>
        (bool)(await ScalarAsync(connection,
            "SELECT EXISTS (SELECT 1 FROM public.\"__EFMigrationsHistory\" WHERE migration_id=@id)",
            ("id", migrationId)))!;
}
