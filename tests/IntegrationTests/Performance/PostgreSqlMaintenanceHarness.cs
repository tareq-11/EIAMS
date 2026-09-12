using System.Data;
using Npgsql;

namespace IntegrationTests.Performance;

/// <summary>
/// Collects a small, read-only maintenance snapshot from the disposable integration-test
/// PostgreSQL container. It is deliberately an evidence harness, not a production tuner: it
/// never changes server settings, creates indexes, runs VACUUM/ANALYZE, or accepts Neon or a
/// non-Test target.
/// </summary>
internal static class PostgreSqlMaintenanceHarness
{
    internal const string GateEnvironmentVariable = "RUN_POSTGRES_MAINTENANCE_HARNESS";
    private const int CommandTimeoutSeconds = 5;
    private const int StatementTimeoutMilliseconds = 5_000;
    private const int MaximumRepresentativePlans = 2;

    internal static async Task<PostgreSqlMaintenanceEvidence> CaptureAsync(
        string connectionString,
        string environmentName,
        PostgreSqlMaintenanceHarnessConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ValidateTestOnlyConnection(connectionString, environmentName);

        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await ExecuteReadOnlyAsync(connection, async (transaction, token) =>
        {
            ForeignKeyIndexSummary foreignKeys = await ReadForeignKeyIndexSummaryAsync(connection, transaction, token)
                .ConfigureAwait(false);
            PostgreSqlMaintenanceStatistics statistics = await ReadStatisticsAsync(connection, transaction, token)
                .ConfigureAwait(false);
            PostgreSqlMaintenanceIndexSummary indexes = await ReadIndexSummaryAsync(connection, transaction, token)
                .ConfigureAwait(false);
            IReadOnlyList<PostgreSqlMaintenancePlanEvidence> plans = await ReadRepresentativePlansAsync(
                connection, transaction, token).ConfigureAwait(false);

            return new PostgreSqlMaintenanceEvidence(
                foreignKeys.MissingSupportingIndexCount == 0 && indexes.InvalidIndexCount == 0 ? "passed" : "review_required",
                configuration.Profile.ToString(),
                new PostgreSqlMaintenanceSafetyBudget(
                    CommandTimeoutSeconds,
                    StatementTimeoutMilliseconds,
                    MaximumRepresentativePlans,
                    "Quick is read-only catalog and plan evidence on the disposable Testcontainers database. Medium is intentionally not implemented; no production tuning is performed."),
                foreignKeys,
                statistics,
                indexes,
                plans,
                "Artifacts contain only aggregate counts, bounded numeric measurements and generic logical labels. They exclude object names, SQL, parameters, IDs, database identity, connection strings and secrets.",
                "Dead tuples are PostgreSQL statistics estimates and are a maintenance signal, not an exact bloat measurement. Short-lived Testcontainers index scan counts are not evidence to remove an index. Representative plans are component evidence only, not production endpoint attribution.");
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static void ValidateTestOnlyConnection(string connectionString, string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (!string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("PostgreSQL maintenance harness requires the explicit Test environment.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, "clean_architecture_integration_test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PostgreSQL maintenance harness requires the disposable Testcontainers integration database.");
        }

        string[] hosts = (builder.Host ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (hosts.Length == 0 || hosts.Any(host => !IsLoopbackHost(host)))
        {
            throw new InvalidOperationException("PostgreSQL maintenance harness accepts only local loopback Testcontainers hosts.");
        }
    }

    internal static ForeignKeyIndexSummary SummarizeForeignKeys(long total, long indexed, long excluded)
    {
        if (total < 0 || indexed < 0 || excluded < 0 || indexed + excluded > total)
        {
            throw new ArgumentOutOfRangeException(nameof(total), "Foreign-key summary counts are inconsistent.");
        }

        return new ForeignKeyIndexSummary(total, indexed, excluded, total - indexed - excluded);
    }

    private static bool IsLoopbackHost(string host)
    {
        string normalized = host.Trim().Trim('[', ']');
        return string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "127.0.0.1", StringComparison.Ordinal) ||
               string.Equals(normalized, "::1", StringComparison.Ordinal);
    }

    private static async Task<T> ExecuteReadOnlyAsync<T>(
        NpgsqlConnection connection,
        Func<NpgsqlTransaction, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await ExecuteNonQueryAsync(connection, transaction, "SET TRANSACTION READ ONLY", cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, transaction,
                $"SET LOCAL statement_timeout = '{StatementTimeoutMilliseconds}ms'", cancellationToken).ConfigureAwait(false);
            return await action(transaction, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal static async Task<ForeignKeyIndexSummary> ReadForeignKeyIndexSummaryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH foreign_keys AS (
                SELECT constraint_row.conrelid, constraint_row.conkey
                FROM pg_constraint AS constraint_row
                INNER JOIN pg_class AS relation ON relation.oid = constraint_row.conrelid
                INNER JOIN pg_namespace AS schema_name ON schema_name.oid = relation.relnamespace
                WHERE constraint_row.contype = 'f'
                  AND schema_name.nspname = 'public'
                  AND relation.relkind IN ('r', 'p')
            )
            SELECT COUNT(*)::bigint,
                   COUNT(*) FILTER (WHERE EXISTS (
                       SELECT 1
                       FROM pg_index AS index_row
                       WHERE index_row.indrelid = foreign_keys.conrelid
                         AND index_row.indisvalid
                         AND index_row.indisready
                         AND index_row.indnkeyatts >= cardinality(foreign_keys.conkey)
                         AND index_row.indpred IS NULL
                         AND NOT EXISTS (
                             SELECT 1
                             FROM unnest(foreign_keys.conkey) WITH ORDINALITY AS foreign_key_column(attnum, position)
                             WHERE index_row.indkey[foreign_key_column.position - 1] IS DISTINCT FROM foreign_key_column.attnum)))::bigint
            FROM foreign_keys
            """;

        await using NpgsqlCommand command = CreateCommand(connection, transaction, sql);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Foreign-key catalog summary returned no row.");
        }

        // No relation is excluded by name. A valid, ready index with the FK columns as its leading
        // prefix (including a PK/unique index) is sufficient for FK delete/update checks.
        return SummarizeForeignKeys(reader.GetInt64(0), reader.GetInt64(1), excluded: 0);
    }

    private static async Task<PostgreSqlMaintenanceStatistics> ReadStatisticsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::bigint,
                   COUNT(*) FILTER (WHERE last_analyze IS NOT NULL OR last_autoanalyze IS NOT NULL)::bigint,
                   COALESCE(SUM(n_live_tup), 0)::bigint,
                   COALESCE(SUM(n_dead_tup), 0)::bigint,
                   COUNT(*) FILTER (WHERE last_autovacuum IS NOT NULL)::bigint
            FROM pg_stat_user_tables
            WHERE schemaname = 'public'
            """;

        await using NpgsqlCommand command = CreateCommand(connection, transaction, sql);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Statistics catalog summary returned no row.");
        }

        return new PostgreSqlMaintenanceStatistics(
            reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4));
    }

    private static async Task<PostgreSqlMaintenanceIndexSummary> ReadIndexSummaryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::bigint,
                   COUNT(*) FILTER (WHERE COALESCE(statistics.idx_scan, 0) > 0)::bigint,
                   COUNT(*) FILTER (WHERE NOT index_row.indisvalid OR NOT index_row.indisready)::bigint,
                   COUNT(*) FILTER (WHERE index_row.indpred IS NOT NULL)::bigint
            FROM pg_stat_user_indexes AS statistics
            INNER JOIN pg_index AS index_row ON index_row.indexrelid = statistics.indexrelid
            WHERE statistics.schemaname = 'public'
            """;

        await using NpgsqlCommand command = CreateCommand(connection, transaction, sql);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Index catalog summary returned no row.");
        }

        return new PostgreSqlMaintenanceIndexSummary(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3));
    }

    private static async Task<IReadOnlyList<PostgreSqlMaintenancePlanEvidence>> ReadRepresentativePlansAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        PostgreSqlMaintenancePlanCandidate[] candidates =
        [
            new("credential-lookup-component",
                "SELECT id, email, status FROM public.users WHERE email = @email",
                command => command.Parameters.AddWithValue("email", IntegrationTestWebAppFactory.AdministratorEmail)),
            new("document-list-component",
                "SELECT id, warehouse_id, document_type, document_status FROM public.warehouse_documents " +
                "WHERE warehouse_id = (SELECT id FROM public.warehouses ORDER BY id LIMIT 1) " +
                "AND document_status = 'Posted' ORDER BY created_at_utc DESC, id LIMIT 20",
                static _ => { })
        ];

        var evidence = new List<PostgreSqlMaintenancePlanEvidence>(candidates.Length);
        foreach (PostgreSqlMaintenancePlanCandidate candidate in candidates)
        {
            await using NpgsqlCommand command = CreateCommand(connection, transaction,
                $"EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) {candidate.Sql}");
            candidate.Configure(command);
            string explainJson = (string)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            evidence.Add(new PostgreSqlMaintenancePlanEvidence(candidate.LogicalName,
                PostgreSqlReadPlanAnalysis.ParseExplainJson(explainJson)));
        }

        return evidence;
    }

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = CreateCommand(connection, transaction, sql);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "All SQL text is private fixed catalog or representative-query constants. The only runtime value is supplied through a typed Npgsql parameter and neither SQL nor parameters are emitted.")]
    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql) => new(sql, connection, transaction)
    {
        CommandTimeout = CommandTimeoutSeconds
    };

    private sealed record PostgreSqlMaintenancePlanCandidate(string LogicalName, string Sql, Action<NpgsqlCommand> Configure);
}

internal sealed record PostgreSqlMaintenanceHarnessConfiguration(PostgreSqlMaintenanceHarnessProfile Profile, string ResultDirectory)
{
    internal static PostgreSqlMaintenanceHarnessConfiguration FromEnvironment(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        if (!string.Equals(getEnvironmentVariable(PostgreSqlMaintenanceHarness.GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{PostgreSqlMaintenanceHarness.GateEnvironmentVariable}=1 is required.");
        }

        return new(ParseProfile(getEnvironmentVariable("EIAMS_POSTGRES_MAINTENANCE_PROFILE")),
            ParseResultDirectory(getEnvironmentVariable("EIAMS_POSTGRES_MAINTENANCE_RESULT_DIR")));
    }

    private static PostgreSqlMaintenanceHarnessProfile ParseProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PostgreSqlMaintenanceHarnessProfile.Quick;
        }
        if (string.Equals(value, "Medium", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("EIAMS_POSTGRES_MAINTENANCE_PROFILE=Medium is not implemented; only bounded Quick is available.");
        }
        if (Enum.TryParse(value, ignoreCase: true, out PostgreSqlMaintenanceHarnessProfile profile) && Enum.IsDefined(profile))
        {
            return profile;
        }
        throw new ArgumentException("EIAMS_POSTGRES_MAINTENANCE_PROFILE must be Quick.");
    }

    private static string ParseResultDirectory(string? value)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "eiams-postgres-maintenance-results"));
        string candidate = string.IsNullOrWhiteSpace(value) ? root : Path.GetFullPath(value);
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(candidate, root, comparison) && !candidate.StartsWith(rootWithSeparator, comparison))
        {
            throw new ArgumentException("EIAMS_POSTGRES_MAINTENANCE_RESULT_DIR must be inside the local temporary eiams-postgres-maintenance-results directory.");
        }
        return candidate;
    }
}

internal enum PostgreSqlMaintenanceHarnessProfile { Quick }

internal sealed record PostgreSqlMaintenanceSafetyBudget(
    int CommandTimeoutSeconds,
    int StatementTimeoutMilliseconds,
    int MaximumRepresentativePlans,
    string Scope);

internal sealed record ForeignKeyIndexSummary(
    long TotalForeignKeyCount,
    long SupportingIndexCount,
    long ExplicitlyExcludedForeignKeyCount,
    long MissingSupportingIndexCount);

internal sealed record PostgreSqlMaintenanceStatistics(
    long UserTableCount,
    long TablesWithAnalyzeStatisticsCount,
    long EstimatedLiveTupleCount,
    long EstimatedDeadTupleCount,
    long TablesAutovacuumedCount);

internal sealed record PostgreSqlMaintenanceIndexSummary(
    long UserIndexCount,
    long IndexesWithObservedScansCount,
    long InvalidIndexCount,
    long PartialIndexCount);

internal sealed record PostgreSqlMaintenancePlanEvidence(string LogicalName, ReadPlanSummary Plan);

internal sealed record PostgreSqlMaintenanceEvidence(
    string Status,
    string Profile,
    PostgreSqlMaintenanceSafetyBudget SafetyBudget,
    ForeignKeyIndexSummary ForeignKeys,
    PostgreSqlMaintenanceStatistics Statistics,
    PostgreSqlMaintenanceIndexSummary Indexes,
    IReadOnlyList<PostgreSqlMaintenancePlanEvidence> RepresentativePlans,
    string DataHandling,
    string Interpretation);

internal static class PostgreSqlMaintenanceArtifactWriter
{
    internal static Task<string> WriteAsync(string directory, PostgreSqlMaintenanceEvidence evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        string fileName = $"postgres-maintenance-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";
        return ApiLoadSuiteArtifactWriter.WriteAsync(directory, fileName, evidence, cancellationToken);
    }
}
