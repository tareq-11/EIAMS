using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Database;

/// <summary>Logs the effective database connection budget without emitting connection-string contents.</summary>
internal sealed class DatabaseConnectionStartupDiagnostics(
    DatabaseConnectionOptions options,
    string connectionString,
    ILogger<DatabaseConnectionStartupDiagnostics> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        DatabaseEndpointKind endpointKind = DatabaseEndpointClassifier.Classify(connectionString);
        logger.LogInformation(
            "PostgreSQL connection budget: pool {MaxPoolSize} × replicas {ReplicaCount} + reserves {Reserves} = {EstimatedMaximumConnections}/{DatabaseConnectionBudget}; endpoint kind {EndpointKind}; connect/sql/lock/request timeouts {ConnectionTimeoutSeconds}/{CommandTimeoutSeconds}/{LockTimeoutSeconds}/{RequestTimeoutSeconds}s.",
            options.MaxPoolSize,
            options.ApplicationReplicaCount,
            options.HealthCheckConnectionReserve + options.MigrationConnectionReserve,
            options.EstimatedMaximumConnections,
            options.DatabaseConnectionBudget,
            endpointKind,
            options.ConnectionTimeoutSeconds,
            options.CommandTimeoutSeconds,
            options.LockTimeoutSeconds,
            options.RequestTimeoutSeconds);

        if (endpointKind == DatabaseEndpointKind.NeonPooled)
        {
            logger.LogInformation(
                "The Neon pooled endpoint manages server connection pressure; it does not eliminate client-to-Neon network latency.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
