using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using SharedKernel;

namespace Infrastructure.Database;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException(
                "Design-time database connection is not configured. " +
                "Set the ConnectionStrings__Database environment variable before running dotnet ef.");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        string boundedConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            // Tooling does not need a long-lived pool. Runtime migrations use the validated
            // DatabasePerformance settings registered by AddInfrastructure.
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 25,
            CancellationTimeout = 2
        }.ConnectionString;

        optionsBuilder
            .UseNpgsql(
                boundedConnectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default))
            .UseSnakeCaseNamingConvention();

        return new ApplicationDbContext(optionsBuilder.Options, new NoOpDomainEventsDispatcher());
    }

    private sealed class NoOpDomainEventsDispatcher : IDomainEventsDispatcher
    {
        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
