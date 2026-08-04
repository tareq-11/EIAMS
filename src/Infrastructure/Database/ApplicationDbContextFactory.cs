using System.Diagnostics.CodeAnalysis;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using SharedKernel;

namespace Infrastructure.Database;

[SuppressMessage("Security", "S2068:Credentials should not be hard-coded", Justification = "Design-time fallback connection string for local development migrations")]
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        optionsBuilder
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=clean-architecture-template;Username=postgres;Password=postgres",
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
