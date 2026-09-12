using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace IntegrationTests.Performance;

/// <summary>
/// Runs a deliberately small, read-only <c>EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)</c>
/// evidence pass against a disposable PostgreSQL test database. It is not an optimizer and it
/// never emits SQL, parameters, database identity, or plan relation names.
/// </summary>
internal static partial class PostgreSqlReadPlanAnalysis
{
    internal const string GateEnvironmentVariable = "RUN_POSTGRES_READ_PLAN_ANALYSIS";
    internal const string LongRunOptInEnvironmentVariable = "EIAMS_ALLOW_LONG_PLAN_ANALYSIS";
    private const int ExplainCommandTimeoutSeconds = 5;
    private const int MaximumCandidates = 3;
    private const int MaximumPlanNodeTypes = 12;
    private const string EvidenceScope = "representative-read-component-plan";
    private const string Interpretation = "Scenario p95 is a selection signal only. This is a representative component plan, not captured production SQL or endpoint-latency attribution; it excludes EF translation and materialization, authorization, cache behavior, HTTP, and other endpoint queries.";

    private static readonly Dictionary<ApiLoadTestScenario, ReadPlanCandidate> Candidates =
        new Dictionary<ApiLoadTestScenario, ReadPlanCandidate>
        {
            [ApiLoadTestScenario.ReadList] = new(
                "organization-list-page-component",
                "SELECT id, name, code, status FROM organizations ORDER BY name, id OFFSET 0 LIMIT 20",
                static (_, _) => { }),
            [ApiLoadTestScenario.ReadDetail] = new(
                "organization-detail-component",
                "SELECT id, name, code, status FROM organizations WHERE id = @organization_id",
                static (command, manifest) => command.Parameters.Add("organization_id", NpgsqlDbType.Uuid)
                    .Value = manifest.GetOrganizationId(0)),
            // This is the fixed warehouse-count read used by the enterprise-scope dashboard path.
            // It is intentionally labelled as a component, not as a plan for the whole report.
            [ApiLoadTestScenario.Report] = new(
                "dashboard-warehouse-count-component",
                "SELECT count(*) FROM warehouses",
                static (_, _) => { })
        };

    internal static IReadOnlyList<ReadPlanSelection> SelectSlowReadCandidates(
        IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics> scenarioMetrics)
    {
        ArgumentNullException.ThrowIfNull(scenarioMetrics);

        return Candidates
            .Where(pair => scenarioMetrics.TryGetValue(pair.Key, out ApiLoadTestScenarioMetrics? metrics) &&
                           metrics.SuccessCount > 0 && metrics.ApproximateP95Ms is not null)
            .Select(pair => new ReadPlanSelection(pair.Value.LogicalName, pair.Key,
                scenarioMetrics[pair.Key].ApproximateP95Ms!.Value))
            .OrderByDescending(selection => selection.ScenarioP95SelectionSignalMs)
            .ThenBy(selection => selection.LogicalName, StringComparer.Ordinal)
            .Take(MaximumCandidates)
            .ToArray();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The command text is composed only from a private fixed candidate catalog; request values are supplied as typed parameters and neither text nor parameters are emitted.")]
    internal static async Task<IReadOnlyList<ReadPlanEvidence>> AnalyzeAsync(
        string connectionString,
        string environmentName,
        SyntheticDatasetManifest manifest,
        IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics> scenarioMetrics,
        CancellationToken cancellationToken = default)
    {
        ValidateTestOnlyConnection(connectionString, environmentName);
        ArgumentNullException.ThrowIfNull(manifest);
        IReadOnlyList<ReadPlanSelection> selections = SelectSlowReadCandidates(scenarioMetrics);

        var evidence = new List<ReadPlanEvidence>(selections.Count);
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (ReadPlanSelection selection in selections)
        {
            ReadPlanCandidate candidate = Candidates[selection.Scenario];
            ReadPlanSummary summary = await ExecuteReadOnlyTransactionAsync(connection, async (transaction, token) =>
            {
                await using var command = new NpgsqlCommand(
                    $"EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) {candidate.Sql}", connection, transaction)
                {
                    CommandTimeout = ExplainCommandTimeoutSeconds
                };
                candidate.ConfigureParameters(command, manifest);
                string explainJson = (string)(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!;
                return ParseExplainJson(explainJson);
            }, cancellationToken).ConfigureAwait(false);
            evidence.Add(new ReadPlanEvidence(
                selection.LogicalName,
                selection.ScenarioP95SelectionSignalMs,
                EvidenceScope,
                Interpretation,
                summary));
        }

        return evidence;
    }

    internal static void ValidateTestOnlyConnection(string connectionString, string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (!string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("PostgreSQL read-plan analysis requires the explicit Test environment.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database) ||
            !builder.Database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("PostgreSQL read-plan analysis requires a database whose name contains 'test'.");
        }

        if ((builder.Host?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Any(host => host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("PostgreSQL read-plan analysis is forbidden against Neon connections.");
        }
    }

    /// <summary>
    /// Runs a test-only database action in an explicit transaction that PostgreSQL marks read-only,
    /// applies a transaction-local statement timeout, then always rolls it back. The caller is internal
    /// test infrastructure and receives no connection identity in its output.
    /// </summary>
    internal static async Task<T> ExecuteReadOnlyTransactionAsync<T>(
        NpgsqlConnection connection,
        Func<NpgsqlTransaction, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("The PostgreSQL connection must be open before read-plan analysis.");
        }

        await using NpgsqlTransaction transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await SetTransactionReadOnlyAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await SetStatementTimeoutAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            return await action(transaction, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal static ReadPlanSummary ParseExplainJson(string explainJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explainJson);
        using var document = JsonDocument.Parse(explainJson);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1)
        {
            throw new FormatException("PostgreSQL EXPLAIN JSON must contain exactly one plan document.");
        }

        JsonElement documentRoot = root[0];
        if (!documentRoot.TryGetProperty("Plan", out JsonElement plan) || plan.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("PostgreSQL EXPLAIN JSON does not contain a root plan.");
        }

        var nodeTypes = new List<string>(MaximumPlanNodeTypes);
        CollectNodeTypes(plan, nodeTypes);
        return new ReadPlanSummary(
            PlanningTimeMs: ReadOptionalNumber(documentRoot, "Planning Time"),
            ExecutionTimeMs: ReadOptionalNumber(documentRoot, "Execution Time"),
            RootTotalCost: ReadOptionalNumber(plan, "Total Cost"),
            RootPlanRows: ReadOptionalLong(plan, "Plan Rows"),
            RootActualRows: ReadOptionalLong(plan, "Actual Rows"),
            RootActualLoops: ReadOptionalLong(plan, "Actual Loops"),
            RootSharedHitBlocks: ReadOptionalLong(plan, "Shared Hit Blocks"),
            RootSharedReadBlocks: ReadOptionalLong(plan, "Shared Read Blocks"),
            RootSharedDirtiedBlocks: ReadOptionalLong(plan, "Shared Dirtied Blocks"),
            RootSharedWrittenBlocks: ReadOptionalLong(plan, "Shared Written Blocks"),
            NodeTypes: nodeTypes);
    }

    private static void CollectNodeTypes(JsonElement plan, ICollection<string> nodeTypes)
    {
        if (nodeTypes.Count >= MaximumPlanNodeTypes)
        {
            return;
        }

        if (plan.TryGetProperty("Node Type", out JsonElement nodeType) && nodeType.ValueKind == JsonValueKind.String)
        {
            nodeTypes.Add(SanitizeNodeType(nodeType.GetString()));
        }

        if (!plan.TryGetProperty("Plans", out JsonElement children) || children.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement child in children.EnumerateArray())
        {
            if (child.ValueKind == JsonValueKind.Object)
            {
                CollectNodeTypes(child, nodeTypes);
            }
        }
    }

    private static string SanitizeNodeType(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 48 && SafeNodeTypePattern().IsMatch(value)
            ? value
            : "redacted";

    private static async Task SetTransactionReadOnlyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction)
        {
            CommandTimeout = ExplainCommandTimeoutSeconds
        };
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SetStatementTimeoutAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SET LOCAL statement_timeout = '5000ms'", connection, transaction)
        {
            CommandTimeout = ExplainCommandTimeoutSeconds
        };
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static double? ReadOptionalNumber(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetDouble(out double result) &&
        !double.IsNaN(result) && !double.IsInfinity(result) && result >= 0 ? result : null;

    private static long? ReadOptionalLong(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt64(out long result) && result >= 0
            ? result
            : null;

    [GeneratedRegex("^[A-Za-z][A-Za-z -]{0,47}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeNodeTypePattern();

    private sealed record ReadPlanCandidate(
        string LogicalName,
        string Sql,
        Action<NpgsqlCommand, SyntheticDatasetManifest> ConfigureParameters);
}

internal enum PostgreSqlReadPlanAnalysisProfile { Quick, Medium }

/// <summary>Environment-only configuration for the explicit, test-only plan evidence pass.</summary>
internal sealed record PostgreSqlReadPlanAnalysisConfiguration(
    PostgreSqlReadPlanAnalysisProfile Profile,
    DatasetProfile DatasetProfile,
    long Seed)
{
    internal static PostgreSqlReadPlanAnalysisConfiguration FromEnvironment(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        if (!string.Equals(getEnvironmentVariable(PostgreSqlReadPlanAnalysis.GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{PostgreSqlReadPlanAnalysis.GateEnvironmentVariable}=1 is required.");
        }

        PostgreSqlReadPlanAnalysisProfile profile = ParseProfile(getEnvironmentVariable("EIAMS_POSTGRES_PLAN_PROFILE"));
        if (profile == PostgreSqlReadPlanAnalysisProfile.Medium &&
            !string.Equals(getEnvironmentVariable(PostgreSqlReadPlanAnalysis.LongRunOptInEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{PostgreSqlReadPlanAnalysis.LongRunOptInEnvironmentVariable}=1 is required for Medium analysis.");
        }

        DatasetProfile dataset = ParseDataset(getEnvironmentVariable("EIAMS_POSTGRES_PLAN_DATASET"), profile);
        bool hasUnexpectedQuickDataset = profile == PostgreSqlReadPlanAnalysisProfile.Quick && dataset != DatasetProfile.Small;
        bool hasUnexpectedMediumDataset = profile == PostgreSqlReadPlanAnalysisProfile.Medium && dataset != DatasetProfile.Medium;
        if (hasUnexpectedQuickDataset || hasUnexpectedMediumDataset)
        {
            throw new InvalidOperationException("Quick uses Small only; Medium uses Medium only. Large is intentionally unsupported.");
        }

        long seed = ParsePositiveLong(getEnvironmentVariable("EIAMS_POSTGRES_PLAN_SEED"), 20260909);
        return new(profile, dataset, seed);
    }

    private static PostgreSqlReadPlanAnalysisProfile ParseProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PostgreSqlReadPlanAnalysisProfile.Quick;
        }
        if (Enum.TryParse(value, true, out PostgreSqlReadPlanAnalysisProfile profile) && Enum.IsDefined(profile))
        {
            return profile;
        }
        throw new ArgumentException("EIAMS_POSTGRES_PLAN_PROFILE must be Quick or Medium.");
    }

    private static DatasetProfile ParseDataset(string? value, PostgreSqlReadPlanAnalysisProfile profile)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return profile == PostgreSqlReadPlanAnalysisProfile.Quick ? DatasetProfile.Small : DatasetProfile.Medium;
        }
        if (Enum.TryParse(value, true, out DatasetProfile dataset) && Enum.IsDefined(dataset))
        {
            return dataset;
        }
        throw new ArgumentException("EIAMS_POSTGRES_PLAN_DATASET must be Small or Medium.");
    }

    private static long ParsePositiveLong(string? value, long fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        if (long.TryParse(value, out long seed) && seed > 0)
        {
            return seed;
        }
        throw new ArgumentException("EIAMS_POSTGRES_PLAN_SEED must be a positive integer.");
    }
}

internal sealed record ReadPlanSelection(string LogicalName, ApiLoadTestScenario Scenario, double ScenarioP95SelectionSignalMs);

/// <summary>Safe evidence only: logical label plus bounded numeric plan properties; never SQL, parameters, IDs, or relation names.</summary>
internal sealed record ReadPlanEvidence(
    string LogicalName,
    double ScenarioP95SelectionSignalMs,
    string EvidenceScope,
    string Interpretation,
    ReadPlanSummary Plan);

internal sealed record ReadPlanSummary(
    double? PlanningTimeMs,
    double? ExecutionTimeMs,
    double? RootTotalCost,
    long? RootPlanRows,
    long? RootActualRows,
    long? RootActualLoops,
    long? RootSharedHitBlocks,
    long? RootSharedReadBlocks,
    long? RootSharedDirtiedBlocks,
    long? RootSharedWrittenBlocks,
    IReadOnlyList<string> NodeTypes);
