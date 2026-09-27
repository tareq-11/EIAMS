using System.Diagnostics.CodeAnalysis;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class UnitOfMeasureStatusMigrationTests(IntegrationTestWebAppFactory factory)
{
    private const string PreviousMigrationId = "20260927103000_EnforcePolymorphicHolderTargets";
    private const string MigrationId = "20260927150833_AddUnitOfMeasureStatus";

    [Fact]
    public async Task Migration_UpAndDown_ShouldDefaultExistingUnitsToActiveAndRemoveStatusOnRollback()
    {
        string database = $"migration_uom_status_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync(PreviousMigrationId);

            var unitId = Guid.NewGuid();
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection,
                "INSERT INTO public.units_of_measure (id, name, symbol, unit_type, created_at_utc) " +
                "VALUES (@id, 'Piece', 'pc', 'Count', NOW())",
                ("id", unitId));

            await context.Database.MigrateAsync();

            (await ColumnExistsAsync(connection, "status")).ShouldBeTrue();
            (await ScalarAsync(connection,
                "SELECT status FROM public.units_of_measure WHERE id = @id",
                ("id", unitId))).ShouldBe("Active");
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeTrue();

            await context.Database.MigrateAsync(PreviousMigrationId);

            (await ColumnExistsAsync(connection, "status")).ShouldBeFalse();
            (await MigrationAppliedAsync(connection, MigrationId)).ShouldBeFalse();
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
        Justification = "The database name is generated from a GUID; SQL row values are bound parameters.")]
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
        Justification = "All SQL text is fixed and all row values are bound parameters.")]
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

    private static async Task<bool> ColumnExistsAsync(NpgsqlConnection connection, string column) =>
        (bool)(await ScalarAsync(connection,
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' " +
            "AND table_name='units_of_measure' AND column_name=@column)",
            ("column", column)))!;

    private static async Task<bool> MigrationAppliedAsync(NpgsqlConnection connection, string migrationId) =>
        (bool)(await ScalarAsync(connection,
            "SELECT EXISTS (SELECT 1 FROM public.\"__EFMigrationsHistory\" WHERE migration_id=@id)",
            ("id", migrationId)))!;
}
