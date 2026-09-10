using Microsoft.Extensions.Options;
using Npgsql;

namespace Infrastructure.Database;

/// <summary>
/// Bounds the number and lifetime of PostgreSQL connections opened by one API process.
/// The pool is client-side: a Neon pooler can reduce server connection pressure, but it does
/// not remove DNS, TCP, TLS, or network latency between this process and Neon.
/// </summary>
public sealed class DatabaseConnectionOptions
{
    public const string SectionName = "DatabasePerformance";

    /// <summary>Enables the Npgsql client-side connection pool.</summary>
    public bool Pooling { get; init; } = true;

    /// <summary>Maximum physical database connections opened by a single API process.</summary>
    public int MaxPoolSize { get; init; } = 20;

    /// <summary>Connections kept ready by a single API process.</summary>
    public int MinPoolSize { get; init; }

    /// <summary>Maximum time spent opening or renting a database connection.</summary>
    public int ConnectionTimeoutSeconds { get; init; } = 15;

    /// <summary>Maximum execution time for one EF/Npgsql command.</summary>
    public int CommandTimeoutSeconds { get; init; } = 25;

    /// <summary>Maximum time PostgreSQL waits for a database lock.</summary>
    public int LockTimeoutSeconds { get; init; } = 20;

    /// <summary>Maximum server-side request duration. It must outlive a SQL command.</summary>
    public int RequestTimeoutSeconds { get; init; } = 30;

    /// <summary>Periodic TCP keepalive for otherwise idle long-lived connections; zero disables it.</summary>
    public int KeepAliveSeconds { get; init; }

    /// <summary>Time Npgsql waits for PostgreSQL to acknowledge command cancellation.</summary>
    public int CancellationTimeoutSeconds { get; init; } = 2;

    /// <summary>Number of concurrently deployed API replicas sharing the database budget.</summary>
    public int ApplicationReplicaCount { get; init; } = 1;

    /// <summary>Maximum connections provisioned for all API replicas and dedicated probes/migrations.</summary>
    public int DatabaseConnectionBudget { get; init; } = 100;

    /// <summary>Connections reserved outside the EF pool for readiness probes.</summary>
    public int HealthCheckConnectionReserve { get; init; } = 1;

    /// <summary>Connections reserved outside the EF pool for a controlled migration runner.</summary>
    public int MigrationConnectionReserve { get; init; } = 1;

    /// <summary>
    /// Optional safety assertion for the configured endpoint: Auto, Direct, or Pooled.
    /// Direct Neon hosts have no <c>-pooler</c> segment; pooled Neon hosts do.
    /// </summary>
    public DatabaseEndpointMode EndpointMode { get; init; } = DatabaseEndpointMode.Auto;

    public long EstimatedMaximumConnections =>
        (long)ApplicationReplicaCount * MaxPoolSize + HealthCheckConnectionReserve + MigrationConnectionReserve;
}

public enum DatabaseEndpointMode
{
    Auto,
    Direct,
    Pooled
}

public enum DatabaseEndpointKind
{
    Other,
    NeonDirect,
    NeonPooled
}

/// <summary>
/// Builds PostgreSQL startup parameters without serializing a full connection string.
/// Existing startup options are retained and EIAMS's validated timeout values are appended last,
/// so PostgreSQL applies the intended final timeout values without a per-checkout <c>SET</c> command.
/// </summary>
public static class PostgresStartupOptions
{
    public static void ApplyTimeouts(NpgsqlConnectionStringBuilder connectionStringBuilder, DatabaseConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(connectionStringBuilder);
        ArgumentNullException.ThrowIfNull(options);

        connectionStringBuilder.Options = MergeTimeouts(
            connectionStringBuilder.Options,
            options.CommandTimeoutSeconds,
            options.LockTimeoutSeconds);
    }

    public static string MergeTimeouts(string? existingOptions, int commandTimeoutSeconds, int lockTimeoutSeconds)
    {
        string preservedOptions = existingOptions?.Trim() ?? string.Empty;
        string requiredOptions =
            $"-c statement_timeout={checked(commandTimeoutSeconds * 1000)} -c lock_timeout={checked(lockTimeoutSeconds * 1000)}";

        return string.IsNullOrEmpty(preservedOptions)
            ? requiredOptions
            : $"{preservedOptions} {requiredOptions}";
    }
}

/// <summary>Validates connection, timeout, and deployment-budget invariants before accepting traffic.</summary>
public sealed class DatabaseConnectionOptionsValidator : IValidateOptions<DatabaseConnectionOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseConnectionOptions options)
    {
        var failures = new List<string>();

        if (!options.Pooling)
        {
            failures.Add(
                "DatabasePerformance:Pooling must remain enabled at runtime because DatabaseConnectionBudget cannot bound unpooled connections. " +
                "The design-time EF factory is intentionally the only unpooled path.");
        }

        if (options.MaxPoolSize is < 1 or > 500)
        {
            failures.Add("DatabasePerformance:MaxPoolSize must be between 1 and 500.");
        }

        if (options.MinPoolSize < 0 || options.MinPoolSize > options.MaxPoolSize)
        {
            failures.Add("DatabasePerformance:MinPoolSize must be between 0 and MaxPoolSize.");
        }

        AddRangeFailure(options.ConnectionTimeoutSeconds, 1, 60, "ConnectionTimeoutSeconds", failures);
        AddRangeFailure(options.CommandTimeoutSeconds, 1, 600, "CommandTimeoutSeconds", failures);
        AddRangeFailure(options.LockTimeoutSeconds, 1, 600, "LockTimeoutSeconds", failures);
        AddRangeFailure(options.RequestTimeoutSeconds, 2, 900, "RequestTimeoutSeconds", failures);
        AddRangeFailure(options.KeepAliveSeconds, 0, 3600, "KeepAliveSeconds", failures);
        AddRangeFailure(options.CancellationTimeoutSeconds, 1, 30, "CancellationTimeoutSeconds", failures);
        AddRangeFailure(options.ApplicationReplicaCount, 1, 100, "ApplicationReplicaCount", failures);
        AddRangeFailure(options.DatabaseConnectionBudget, 1, 10_000, "DatabaseConnectionBudget", failures);
        AddRangeFailure(options.HealthCheckConnectionReserve, 0, 100, "HealthCheckConnectionReserve", failures);
        AddRangeFailure(options.MigrationConnectionReserve, 0, 100, "MigrationConnectionReserve", failures);

        if (options.LockTimeoutSeconds > options.CommandTimeoutSeconds)
        {
            failures.Add("DatabasePerformance:LockTimeoutSeconds must not exceed CommandTimeoutSeconds.");
        }

        if (options.ConnectionTimeoutSeconds > options.CommandTimeoutSeconds)
        {
            failures.Add("DatabasePerformance:ConnectionTimeoutSeconds must not exceed CommandTimeoutSeconds.");
        }

        if (options.RequestTimeoutSeconds < options.CommandTimeoutSeconds + options.CancellationTimeoutSeconds)
        {
            failures.Add(
                "DatabasePerformance:RequestTimeoutSeconds must be at least CommandTimeoutSeconds plus CancellationTimeoutSeconds.");
        }

        if (options.EstimatedMaximumConnections > options.DatabaseConnectionBudget)
        {
            failures.Add(
                "DatabasePerformance connection budget is too small for MaxPoolSize × ApplicationReplicaCount plus health and migration reserves.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void AddRangeFailure(int value, int minimum, int maximum, string propertyName, List<string> failures)
    {
        if (value < minimum || value > maximum)
        {
            failures.Add($"DatabasePerformance:{propertyName} must be between {minimum} and {maximum}.");
        }
    }
}

/// <summary>Classifies a connection target without exposing credentials or other connection-string values.</summary>
public static class DatabaseEndpointClassifier
{
    public static DatabaseEndpointKind Classify(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        string host = builder.Host ?? string.Empty;
        if (!host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseEndpointKind.Other;
        }

        return host.Contains("-pooler", StringComparison.OrdinalIgnoreCase)
            ? DatabaseEndpointKind.NeonPooled
            : DatabaseEndpointKind.NeonDirect;
    }

    public static void ValidateExpectedMode(string connectionString, DatabaseEndpointMode expectedMode)
    {
        if (expectedMode == DatabaseEndpointMode.Auto)
        {
            return;
        }

        DatabaseEndpointKind actual = Classify(connectionString);
        bool matches = expectedMode switch
        {
            DatabaseEndpointMode.Direct => actual is DatabaseEndpointKind.NeonDirect or DatabaseEndpointKind.Other,
            DatabaseEndpointMode.Pooled => actual == DatabaseEndpointKind.NeonPooled,
            _ => true
        };

        if (!matches)
        {
            throw new InvalidOperationException(
                $"DatabasePerformance:EndpointMode is '{expectedMode}' but the configured database endpoint is '{actual}'. " +
                "Use Direct for a direct Neon host and Pooled only for a Neon host containing '-pooler'.");
        }
    }
}
