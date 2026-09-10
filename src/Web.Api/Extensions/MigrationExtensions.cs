using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Web.Api.Extensions;

public static class MigrationExtensions
{
    private const string MigrationLockResource = "eiams:ef-migrations";

    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        using ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The database connection string is required before migrations can run.");

        // EF's migration lock is scoped to its own migration connection. Holding this separate
        // session-level PostgreSQL lock also prevents two application instances from each
        // reaching Migrate during startup, including non-transactional concurrent-index steps.
        using var migrationLockConnection = new NpgsqlConnection(connectionString);
        migrationLockConnection.Open();

        using var acquireLock = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(hashtextextended(@resource, 0))",
            migrationLockConnection);
        acquireLock.Parameters.AddWithValue("resource", MigrationLockResource);

        if (acquireLock.ExecuteScalar() is not bool lockAcquired || !lockAcquired)
        {
            throw new InvalidOperationException(
                "Another EF migration runner currently holds the EIAMS migration advisory lock. " +
                "Do not start this instance until that migration finishes or is recovered.");
        }

        try
        {
            dbContext.Database.Migrate();
        }
        finally
        {
            using var releaseLock = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(hashtextextended(@resource, 0))",
                migrationLockConnection);
            releaseLock.Parameters.AddWithValue("resource", MigrationLockResource);
            releaseLock.ExecuteScalar();
        }
    }
}
