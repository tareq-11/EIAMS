using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace IntegrationTests.Performance;

/// <summary>
/// Collects a deliberately small pg_stat_statements evidence sample from the disposable
/// integration-test PostgreSQL container. It never accepts a production or Neon target and it
/// never writes SQL text, parameter values, database identifiers, or credentials to artifacts.
/// </summary>
internal static class PgStatStatementsHarness
{
    internal const string GateEnvironmentVariable = "RUN_PG_STAT_STATEMENTS_HARNESS";
    internal const string LongRunOptInEnvironmentVariable = "EIAMS_ALLOW_LONG_PG_STAT_STATEMENTS_HARNESS";
    private const int CommandTimeoutSeconds = 5;
    private const int MaximumTopStatements = 10;
    private const int QuickWorkloadExecutions = 6;
    private const int MediumWorkloadExecutions = 60;

    internal static async Task<PgStatStatementsEvidence> CaptureQuickAsync(
        string connectionString,
        string environmentName,
        PgStatStatementsHarnessConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ValidateTestOnlyConnection(connectionString, environmentName);

        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        PgStatStatementsServerInfo server = await EnsureAvailableAsync(connection, cancellationToken).ConfigureAwait(false);
        await ResetAsync(connection, cancellationToken).ConfigureAwait(false);
        await RunBoundedRepresentativeWorkloadAsync(connection, configuration.WorkloadExecutions, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<PgStatStatementsStatement> top = await ReadTopStatementsAsync(connection, cancellationToken)
            .ConfigureAwait(false);

        return new PgStatStatementsEvidence(
            "passed",
            configuration.Profile.ToString(),
            server,
            new PgStatStatementsSafetyBudget(configuration.WorkloadExecutions, MaximumTopStatements, CommandTimeoutSeconds,
                "The harness only accepts the disposable Testcontainers integration database. Medium requires an additional opt-in; Full is not implemented."),
            top,
            "Statement fingerprints are one-way SHA-256 labels derived in memory from PostgreSQL normalized query text. The artifact excludes raw SQL, parameters, database identity, IDs, connection strings and secrets.",
            "Metrics cover only the bounded representative SQL workload after pg_stat_statements_reset. They are not endpoint latency attribution or production evidence.");
    }

    internal static void ValidateTestOnlyConnection(string connectionString, string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (!string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("pg_stat_statements harness requires the explicit Test environment.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database) ||
            !builder.Database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("pg_stat_statements harness requires a database whose name contains 'test'.");
        }

        string[] hosts = (builder.Host ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (hosts.Length == 0 || hosts.Any(host => !IsLoopbackHost(host)))
        {
            throw new InvalidOperationException("pg_stat_statements harness accepts only local loopback Testcontainers hosts.");
        }
    }

    private static bool IsLoopbackHost(string host)
    {
        string normalized = host.Trim().Trim('[', ']');
        return string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "127.0.0.1", StringComparison.Ordinal) ||
               string.Equals(normalized, "::1", StringComparison.Ordinal);
    }

    internal static async Task<PgStatStatementsServerInfo> EnsureAvailableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await ExecuteNonQueryAsync(connection, "CREATE EXTENSION IF NOT EXISTS pg_stat_statements", cancellationToken)
            .ConfigureAwait(false);

        const string serverSql = """
            SELECT current_setting('server_version'),
                   current_setting('shared_preload_libraries'),
                   extversion
            FROM pg_extension
            WHERE extname = 'pg_stat_statements'
            """;
        string serverVersion;
        string preloadLibraries;
        string extensionVersion;
        await using (NpgsqlCommand command = CreateCommand(connection, serverSql))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("pg_stat_statements extension was not available in the Testcontainers server.");
            }

            serverVersion = reader.GetString(0);
            preloadLibraries = reader.GetString(1);
            extensionVersion = reader.GetString(2);
        }
        if (!preloadLibraries.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Contains("pg_stat_statements", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Testcontainers PostgreSQL was not started with shared_preload_libraries=pg_stat_statements.");
        }

        string[] columns = await GetSupportedColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        string[] required = ["query", "calls", "total_exec_time", "mean_exec_time", "rows"];
        if (required.Except(columns, StringComparer.Ordinal).Any())
        {
            throw new InvalidOperationException("The installed pg_stat_statements extension does not expose the required current timing columns.");
        }

        return new PgStatStatementsServerInfo(serverVersion, extensionVersion, columns);
    }

    internal static IReadOnlyList<PgStatStatementsStatement> ParseRows(IEnumerable<PgStatStatementsRawRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows
            .Where(row => row.Calls > 0 && row.TotalExecutionTimeMs >= 0 && row.MeanExecutionTimeMs >= 0 && row.Rows >= 0)
            .OrderByDescending(row => row.TotalExecutionTimeMs)
            .ThenBy(row => Fingerprint(row.NormalizedQuery), StringComparer.Ordinal)
            .Take(MaximumTopStatements)
            .Select(row => new PgStatStatementsStatement(
                Fingerprint(row.NormalizedQuery), row.Calls, row.TotalExecutionTimeMs,
                row.MeanExecutionTimeMs, row.Rows))
            .ToArray();
    }

    internal static string Fingerprint(string normalizedQuery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedQuery);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuery));
        return $"sql-sha256-{Convert.ToHexString(hash.AsSpan(0, 12))}";
    }

    private static async Task<string[]> GetSupportedColumnsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'pg_stat_statements'
            ORDER BY ordinal_position
            """;
        await using NpgsqlCommand command = CreateCommand(connection, sql);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<string> columns = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(reader.GetString(0));
        }
        return columns.ToArray();
    }

    private static Task ResetAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ExecuteNonQueryAsync(connection, "SELECT pg_stat_statements_reset()", cancellationToken);

    private static async Task RunBoundedRepresentativeWorkloadAsync(
        NpgsqlConnection connection,
        int executions,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index < executions; index++)
        {
            await ExecuteNonQueryAsync(connection,
                "SELECT id, email FROM public.users WHERE email = @email",
                cancellationToken,
                command => command.Parameters.AddWithValue("email", "integration-admin@example.com"))
                .ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection,
                "SELECT id, name, code FROM public.organizations ORDER BY name, id OFFSET 0 LIMIT 20",
                cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<PgStatStatementsStatement>> ReadTopStatementsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT query, calls, total_exec_time, mean_exec_time, rows
            FROM pg_stat_statements
            WHERE dbid = (SELECT oid FROM pg_database WHERE datname = current_database())
            ORDER BY total_exec_time DESC, queryid ASC
            LIMIT 10
            """;
        await using NpgsqlCommand command = CreateCommand(connection, sql);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<PgStatStatementsRawRow> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new PgStatStatementsRawRow(
                reader.GetString(0), reader.GetInt64(1), reader.GetDouble(2), reader.GetDouble(3), reader.GetInt64(4)));
        }
        return ParseRows(rows);
    }

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken,
        Action<NpgsqlCommand>? configure = null)
    {
        await using NpgsqlCommand command = CreateCommand(connection, sql);
        configure?.Invoke(command);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "All callers pass fixed private SQL constants; no user value is composed into command text and the only runtime value is supplied as a typed parameter.")]
    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql) => new(sql, connection)
    {
        CommandTimeout = CommandTimeoutSeconds
    };

    internal static int GetWorkloadExecutions(PgStatStatementsHarnessProfile profile) => profile switch
    {
        PgStatStatementsHarnessProfile.Quick => QuickWorkloadExecutions,
        PgStatStatementsHarnessProfile.Medium => MediumWorkloadExecutions,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
    };
}

internal sealed record PgStatStatementsHarnessConfiguration(
    PgStatStatementsHarnessProfile Profile,
    int WorkloadExecutions,
    string ResultDirectory)
{
    internal static PgStatStatementsHarnessConfiguration FromEnvironment(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        if (!string.Equals(getEnvironmentVariable(PgStatStatementsHarness.GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{PgStatStatementsHarness.GateEnvironmentVariable}=1 is required.");
        }

        PgStatStatementsHarnessProfile profile = ParseProfile(getEnvironmentVariable("EIAMS_PG_STAT_STATEMENTS_PROFILE"));
        if (profile == PgStatStatementsHarnessProfile.Medium &&
            !string.Equals(getEnvironmentVariable(PgStatStatementsHarness.LongRunOptInEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{PgStatStatementsHarness.LongRunOptInEnvironmentVariable}=1 is required for Medium.");
        }

        return new(profile, PgStatStatementsHarness.GetWorkloadExecutions(profile),
            ParseResultDirectory(getEnvironmentVariable("EIAMS_PG_STAT_STATEMENTS_RESULT_DIR")));
    }

    private static PgStatStatementsHarnessProfile ParseProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PgStatStatementsHarnessProfile.Quick;
        }
        if (Enum.TryParse(value, ignoreCase: true, out PgStatStatementsHarnessProfile profile) && Enum.IsDefined(profile))
        {
            return profile;
        }
        throw new ArgumentException($"EIAMS_PG_STAT_STATEMENTS_PROFILE must be one of: {string.Join(", ", Enum.GetNames<PgStatStatementsHarnessProfile>())}.");
    }

    private static string ParseResultDirectory(string? value)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "eiams-pg-stat-statements-results"));
        string candidate = string.IsNullOrWhiteSpace(value) ? root : Path.GetFullPath(value);
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(candidate, root, comparison) && !candidate.StartsWith(rootWithSeparator, comparison))
        {
            throw new ArgumentException("EIAMS_PG_STAT_STATEMENTS_RESULT_DIR must be inside the local temporary eiams-pg-stat-statements-results directory.");
        }
        return candidate;
    }
}

internal enum PgStatStatementsHarnessProfile { Quick, Medium }

internal sealed record PgStatStatementsServerInfo(
    string PostgreSqlVersion,
    string ExtensionVersion,
    IReadOnlyList<string> SupportedColumns);

internal sealed record PgStatStatementsRawRow(
    string NormalizedQuery,
    long Calls,
    double TotalExecutionTimeMs,
    double MeanExecutionTimeMs,
    long Rows);

internal sealed record PgStatStatementsStatement(
    string Fingerprint,
    long Calls,
    double TotalExecutionTimeMs,
    double MeanExecutionTimeMs,
    long Rows);

internal sealed record PgStatStatementsSafetyBudget(
    int WorkloadExecutions,
    int MaximumTopStatements,
    int CommandTimeoutSeconds,
    string EvidenceLimit);

internal sealed record PgStatStatementsEvidence(
    string Status,
    string Profile,
    PgStatStatementsServerInfo Server,
    PgStatStatementsSafetyBudget SafetyBudget,
    IReadOnlyList<PgStatStatementsStatement> TopStatements,
    string DataHandling,
    string Interpretation);

internal static class PgStatStatementsArtifactWriter
{
    internal static async Task<string> WriteAsync(
        string directory,
        PgStatStatementsEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Directory.CreateDirectory(directory);
        string fileName = $"pg-stat-statements-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";
        string destination = Path.Combine(directory, fileName);
        string temporary = Path.Combine(directory, $".{fileName}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(evidence), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, destination, overwrite: false);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
