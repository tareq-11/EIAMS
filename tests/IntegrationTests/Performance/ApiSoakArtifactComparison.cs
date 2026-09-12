using System.Text.Json;

namespace IntegrationTests.Performance;

internal static class ApiSoakArtifactComparison
{
    internal static ApiSoakArtifactComparisonResult Compare(string baselineJson, string candidateJson, ApiSoakComparisonThresholds? thresholds = null)
    {
        var baseline = ApiSoakArtifact.Parse(baselineJson, "baseline");
        var candidate = ApiSoakArtifact.Parse(candidateJson, "candidate");
        EnsureComparable(baseline, candidate);
        thresholds ??= ApiSoakComparisonThresholds.Default;
        thresholds.Validate();
        var metrics = new List<ApiSoakComparisonMetric>
        {
            LowerIsBetter("http_p50_ms", baseline.Http.P50Ms, candidate.Http.P50Ms, thresholds.LatencyRegressionPercent),
            LowerIsBetter("http_p95_ms", baseline.Http.P95Ms, candidate.Http.P95Ms, thresholds.LatencyRegressionPercent),
            LowerIsBetter("http_p99_ms", baseline.Http.P99Ms, candidate.Http.P99Ms, thresholds.LatencyRegressionPercent),
            HigherIsBetter("successful_throughput_requests_per_second", baseline.Http.ThroughputRequestsPerSecond, candidate.Http.ThroughputRequestsPerSecond, thresholds.ThroughputRegressionPercent),
            Failure("unexpected_http", baseline.Http.UnexpectedHttp, candidate.Http.UnexpectedHttp),
            Failure("rate_limited", baseline.Http.RateLimited, candidate.Http.RateLimited),
            Failure("timeout_or_cancellation", baseline.Http.TimeoutOrCancellation, candidate.Http.TimeoutOrCancellation),
            Failure("transport_failures", baseline.Http.TransportFailures, candidate.Http.TransportFailures),
            Failure("pool_timeouts", baseline.Pool.PoolTimeouts, candidate.Pool.PoolTimeouts),
            Observational("managed_memory_observed_end_minus_start_bytes", baseline.Memory.ObservedEndMinusStartBytes, candidate.Memory.ObservedEndMinusStartBytes),
            Observational("managed_memory_peak_bytes", baseline.Memory.PeakBytes, candidate.Memory.PeakBytes),
            Observational("pool_used_connections", baseline.Pool.UsedConnections, candidate.Pool.UsedConnections),
            Observational("pool_max_connections", baseline.Pool.MaxConnections, candidate.Pool.MaxConnections),
            Observational("recovery_pool_used_connections", baseline.RecoveryPool.UsedConnections, candidate.RecoveryPool.UsedConnections),
            Observational("recovery_pool_max_connections", baseline.RecoveryPool.MaxConnections, candidate.RecoveryPool.MaxConnections),
            Observational("sampled_lock_wait_observations", baseline.Locks.TotalWaitingSessionObservations, candidate.Locks.TotalWaitingSessionObservations),
            Observational("sampled_lock_wait_max_concurrent_sessions", baseline.Locks.MaxConcurrentWaitingSessions, candidate.Locks.MaxConcurrentWaitingSessions),
            Observational("sampled_lock_wait_approximate_milliseconds", baseline.Locks.ApproximateObservedWaitingSessionMilliseconds, candidate.Locks.ApproximateObservedWaitingSessionMilliseconds)
        };
        return new(ApiSoakArtifactComparisonSchema.Version, metrics.Any(metric => metric.IsRegression) ? "regression_detected" : "no_threshold_regression_detected", metrics);
    }

    internal static async Task<string> WriteValidatedAsync(string directory, string fileName, ApiSoakArtifactComparisonResult comparison, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        var artifact = new
        {
            SchemaVersion = ApiSoakArtifactComparisonSchema.Version,
            Status = "completed",
            Limitations = "One matched pair is evidence only; repeat equivalent runs before making a performance conclusion. Memory and sampled lock-wait values are observational.",
            comparison.Outcome,
            comparison.Metrics
        };
        return await ApiSoakArtifactWriter.WriteValidatedAsync(directory, fileName, artifact, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureComparable(ApiSoakArtifact baseline, ApiSoakArtifact candidate)
    {
        EnsureEqual("suite profile", baseline.Execution.SuiteProfile, candidate.Execution.SuiteProfile);
        EnsureEqual("dataset profile", baseline.Dataset.Profile, candidate.Dataset.Profile);
        EnsureEqual("dataset seed", baseline.Dataset.Seed, candidate.Dataset.Seed);
        EnsureEqual("worker profile semantics", baseline.WorkerProfileContract, candidate.WorkerProfileContract);
        EnsureEqual("runtime", baseline.Environment.Runtime, candidate.Environment.Runtime);
        EnsureEqual("operating system", baseline.Environment.OperatingSystem, candidate.Environment.OperatingSystem);
        EnsureEqual("transport", baseline.Environment.Transport, candidate.Environment.Transport);
        EnsureEqual("TLS", baseline.Environment.Tls, candidate.Environment.Tls);
        EnsureEqual("warmup duration", baseline.Execution.WarmupSeconds, candidate.Execution.WarmupSeconds);
        EnsureEqual("measurement duration", baseline.Execution.MeasurementSeconds, candidate.Execution.MeasurementSeconds);
        EnsureEqual("client concurrency", baseline.Execution.ClientConcurrency, candidate.Execution.ClientConcurrency);
        EnsureEqual("load mode", baseline.Execution.LoadMode, candidate.Execution.LoadMode);
        EnsureEqual("arrival rate", baseline.Execution.ArrivalRatePerSecond, candidate.Execution.ArrivalRatePerSecond);
        EnsureEqual("run seed", baseline.Execution.RunSeed, candidate.Execution.RunSeed);
        EnsureEqual("maximum started requests", baseline.Execution.MaximumStartedRequests, candidate.Execution.MaximumStartedRequests);
        EnsureEqual("maximum post requests", baseline.Execution.MaximumPostRequests, candidate.Execution.MaximumPostRequests);
        EnsureEqual("scenario mix", baseline.Execution.ScenarioMixContract, candidate.Execution.ScenarioMixContract);
        EnsureEqual("scenario contract", baseline.ScenarioContract, candidate.ScenarioContract);
        EnsureEqual("pool count availability", baseline.Pool.ConnectionCountAvailable, candidate.Pool.ConnectionCountAvailable);
        EnsureEqual("pool maximum availability", baseline.Pool.ConnectionMaxAvailable, candidate.Pool.ConnectionMaxAvailable);
        EnsureEqual("pool timeout availability", baseline.Pool.ConnectionTimeoutsAvailable, candidate.Pool.ConnectionTimeoutsAvailable);
        EnsureEqual("recovery pool count availability", baseline.RecoveryPool.ConnectionCountAvailable, candidate.RecoveryPool.ConnectionCountAvailable);
        EnsureEqual("recovery pool maximum availability", baseline.RecoveryPool.ConnectionMaxAvailable, candidate.RecoveryPool.ConnectionMaxAvailable);
        EnsureEqual("recovery pool timeout availability", baseline.RecoveryPool.ConnectionTimeoutsAvailable, candidate.RecoveryPool.ConnectionTimeoutsAvailable);
        EnsureEqual("lock sampler availability", baseline.Locks.IsAvailable, candidate.Locks.IsAvailable);
        EnsureEqual("lock sampler interval", baseline.Locks.SamplingIntervalMilliseconds, candidate.Locks.SamplingIntervalMilliseconds);
    }

    private static ApiSoakComparisonMetric LowerIsBetter(string name, double baseline, double candidate, double thresholdPercent)
    {
        double? percent = PercentChange(baseline, candidate);
        bool regression = percent is not null && percent.Value > thresholdPercent;
        return new(name, "lower_is_better", "threshold", baseline, candidate, percent, regression, regression ? "regression" : "informational");
    }

    private static ApiSoakComparisonMetric HigherIsBetter(string name, double baseline, double candidate, double thresholdPercent)
    {
        double? percent = PercentChange(baseline, candidate);
        bool regression = percent is not null && percent.Value < -thresholdPercent;
        return new(name, "higher_is_better", "threshold", baseline, candidate, percent, regression, regression ? "regression" : "informational");
    }

    private static ApiSoakComparisonMetric Failure(string name, long baseline, long candidate) => new(name, "lower_is_better", "zero_tolerance_increase", baseline, candidate, PercentChange(baseline, candidate), candidate > baseline, candidate > baseline ? "regression" : "informational");
    private static ApiSoakComparisonMetric Observational(string name, double baseline, double candidate) => new(name, "observational", "none", baseline, candidate, PercentChange(baseline, candidate), false, "observational");
    private static double? PercentChange(double baseline, double candidate) => Math.Abs(baseline) < double.Epsilon ? null : (candidate - baseline) / baseline * 100;

    private static void EnsureEqual<T>(string name, T baseline, T candidate)
    {
        if (!EqualityComparer<T>.Default.Equals(baseline, candidate))
        {
            throw new ArgumentException($"Artifacts are not comparable: {name} differs.");
        }
    }
}

internal sealed record ApiSoakComparisonThresholds(double LatencyRegressionPercent, double ThroughputRegressionPercent)
{
    internal static readonly ApiSoakComparisonThresholds Default = new(10, 10);
    internal void Validate()
    {
        if (!double.IsFinite(LatencyRegressionPercent) || !double.IsFinite(ThroughputRegressionPercent) || LatencyRegressionPercent is < 0 or > 100 || ThroughputRegressionPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(LatencyRegressionPercent), "Comparison thresholds must be finite percentages between zero and one hundred.");
        }
    }
}

internal sealed record ApiSoakComparisonMetric(string Name, string Direction, string RegressionRule, double Baseline, double Candidate, double? PercentChange, bool IsRegression, string Assessment);
internal sealed record ApiSoakArtifactComparisonResult(string SchemaVersion, string Outcome, IReadOnlyList<ApiSoakComparisonMetric> Metrics)
{
    internal bool HasRegression => Metrics.Any(metric => metric.IsRegression);
}
internal static class ApiSoakArtifactComparisonSchema { internal const string Version = "api_soak_comparison_v1"; internal const string ArtifactVersion = "api_soak_artifact_v2"; }

internal sealed record ApiSoakArtifact(string Status, ApiSoakExecutionContract Execution, ApiSoakDatasetContract Dataset, string WorkerProfileContract, ApiSoakEnvironmentContract Environment, ApiSoakHttpMetrics Http, string ScenarioContract, ApiSoakMemoryObservation Memory, ApiSoakPoolEvidence Pool, ApiSoakPoolEvidence RecoveryPool, ApiSoakLockEvidence Locks, ApiSoakRecoveryEvidence Recovery)
{
    internal static ApiSoakArtifact Parse(string json, string artifactName)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            RequireObject(root, artifactName);
            if (!string.Equals(RequiredString(root, "SchemaVersion", artifactName), ApiSoakArtifactComparisonSchema.ArtifactVersion, StringComparison.Ordinal))
            {
                throw Invalid(artifactName, "SchemaVersion is unsupported.");
            }
            var execution = ApiSoakExecutionContract.Parse(RequiredObject(root, "ExecutionProfile", artifactName), artifactName);
            JsonElement scenarioMetrics = RequiredObject(root, "ScenarioMetrics", artifactName);
            string scenarioContract = ParseScenarioContract(scenarioMetrics, execution, artifactName);
            var http = ApiSoakHttpMetrics.Parse(RequiredObject(root, "Http", artifactName), artifactName);
            EnsureScenarioAggregate(scenarioMetrics, http, artifactName);
            var artifact = new ApiSoakArtifact(RequiredString(root, "Status", artifactName), execution, ApiSoakDatasetContract.Parse(RequiredObject(root, "Dataset", artifactName), artifactName), CanonicalObject(RequiredObject(root, "WorkerProfile", artifactName)), ApiSoakEnvironmentContract.Parse(RequiredObject(root, "Environment", artifactName), artifactName), http, scenarioContract, ApiSoakMemoryObservation.Parse(RequiredObject(root, "ManagedMemory", artifactName), artifactName), ApiSoakPoolEvidence.Parse(RequiredObject(root, "NpgsqlPool", artifactName), artifactName), ApiSoakPoolEvidence.Parse(RequiredObject(root, "RecoveryNpgsqlPool", artifactName), artifactName), ApiSoakLockEvidence.Parse(RequiredObject(root, "PostgreSqlSampledLockWaitOccupancy", artifactName), artifactName), ApiSoakRecoveryEvidence.Parse(RequiredObject(root, "Recovery", artifactName), artifactName));
            artifact.ValidatePassed(artifactName);
            return artifact;
        }
        catch (JsonException exception) { throw Invalid(artifactName, $"JSON is malformed: {exception.GetType().Name}."); }
    }

    private void ValidatePassed(string artifactName)
    {
        if (!string.Equals(Status, "passed", StringComparison.Ordinal))
        {
            throw Invalid(artifactName, "Status must be passed.");
        }
        if (Http.Successful > Http.Completed || Http.Completed == 0 || Http.ThroughputRequestsPerSecond <= 0 || Http.HasFailure)
        {
            throw Invalid(artifactName, "passed HTTP evidence is inconsistent or contains failures.");
        }
        if (!Pool.IsFullyAvailable || Pool.UsedConnections > Pool.MaxConnections || Pool.PoolTimeouts != 0 || !RecoveryPool.IsFullyAvailable || RecoveryPool.UsedConnections > RecoveryPool.MaxConnections || RecoveryPool.PoolTimeouts != 0)
        {
            throw Invalid(artifactName, "passed pool evidence is unavailable, saturated, or timed out.");
        }
        if (!Locks.IsAvailable || Locks.FailureCount != 0 || Locks.SampleCount == 0 || Locks.SamplesWithLockWaits > Locks.SampleCount || Locks.MaxConcurrentWaitingSessions > Locks.TotalWaitingSessionObservations)
        {
            throw Invalid(artifactName, "passed sampled lock-wait evidence is inconsistent.");
        }
        if (!Recovery.IsPassed)
        {
            throw Invalid(artifactName, "passed recovery evidence is required.");
        }
    }

    private static string ParseScenarioContract(JsonElement scenarios, ApiSoakExecutionContract execution, string artifactName)
    {
        RequireObject(scenarios, artifactName);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty scenario in scenarios.EnumerateObject())
        {
            if (scenario.Value.ValueKind != JsonValueKind.Object)
            {
                throw Invalid(artifactName, "ScenarioMetrics has an unknown or invalid scenario.");
            }
            long count = RequiredLong(scenario.Value, "Count", artifactName);
            long successful = RequiredLong(scenario.Value, "SuccessCount", artifactName);
            long failures = RequiredLong(scenario.Value, "FailureCount", artifactName);
            if (successful > count || failures > count || successful + failures != count)
            {
                throw Invalid(artifactName, "ScenarioMetrics counts are inconsistent.");
            }
            if (!execution.ScenarioNames.Contains(scenario.Name))
            {
                if (scenario.Name == nameof(ApiLoadTestScenario.Login) && count == 0)
                {
                    continue;
                }
                throw Invalid(artifactName, "ScenarioMetrics has an unknown or invalid scenario.");
            }
            if (!names.Add(scenario.Name))
            {
                throw Invalid(artifactName, "ScenarioMetrics contains duplicate configured scenarios.");
            }
        }
        if (!names.SetEquals(execution.ScenarioNames))
        {
            throw Invalid(artifactName, "ScenarioMetrics must exactly match ScenarioMix.");
        }
        return execution.ScenarioMixContract;
    }

    private static void EnsureScenarioAggregate(JsonElement scenarios, ApiSoakHttpMetrics http, string artifactName)
    {
        long completed = 0;
        long successful = 0;
        foreach (JsonProperty scenario in scenarios.EnumerateObject())
        {
            completed += RequiredLong(scenario.Value, "Count", artifactName);
            successful += RequiredLong(scenario.Value, "SuccessCount", artifactName);
        }
        if (completed != http.Completed || successful != http.Successful)
        {
            throw Invalid(artifactName, "ScenarioMetrics aggregate does not match HTTP totals.");
        }
    }

    internal static JsonElement RequiredObject(JsonElement parent, string name, string artifactName) => RequiredValue(parent, name, artifactName, JsonValueKind.Object);
    internal static string RequiredString(JsonElement parent, string name, string artifactName) => RequiredValue(parent, name, artifactName, JsonValueKind.String).GetString()!;
    internal static long RequiredLong(JsonElement parent, string name, string artifactName)
    {
        long number = RequiredValue(parent, name, artifactName).GetInt64();
        return number < 0 ? throw Invalid(artifactName, $"{name} must be a non-negative integer.") : number;
    }
    internal static long RequiredPositiveLong(JsonElement parent, string name, string artifactName)
    {
        long value = RequiredLong(parent, name, artifactName);
        return value == 0 ? throw Invalid(artifactName, $"{name} must be positive.") : value;
    }
    internal static long RequiredSignedLong(JsonElement parent, string name, string artifactName) => RequiredValue(parent, name, artifactName).GetInt64();
    internal static double RequiredDouble(JsonElement parent, string name, string artifactName)
    {
        double number = RequiredValue(parent, name, artifactName).GetDouble();
        return !double.IsFinite(number) || number < 0 ? throw Invalid(artifactName, $"{name} must be a finite non-negative number.") : number;
    }
    internal static double RequiredPositiveDouble(JsonElement parent, string name, string artifactName)
    {
        double value = RequiredDouble(parent, name, artifactName);
        return value <= 0 ? throw Invalid(artifactName, $"{name} must be positive.") : value;
    }
    internal static bool RequiredBoolean(JsonElement parent, string name, string artifactName) => RequiredValue(parent, name, artifactName).GetBoolean();
    internal static void RequireObject(JsonElement value, string artifactName)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(artifactName, "root must be an object.");
        }
    }
    internal static string CanonicalObject(JsonElement value) => JsonSerializer.Serialize(value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => new KeyValuePair<string, string>(property.Name, property.Value.GetRawText())), SerializationOptions);
    internal static ArgumentException Invalid(string artifactName, string reason) => new($"Invalid {artifactName} soak artifact: {reason}");
    private static readonly JsonSerializerOptions SerializationOptions = new() { WriteIndented = false };

    private static JsonElement RequiredValue(JsonElement parent, string name, string artifactName, JsonValueKind? expected = null)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || expected is not null && value.ValueKind != expected || expected == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw Invalid(artifactName, $"{name} has an invalid value.");
        }
        return value;
    }
}

internal sealed record ApiSoakExecutionContract(string SuiteProfile, double WarmupSeconds, double MeasurementSeconds, long ClientConcurrency, string LoadMode, double? ArrivalRatePerSecond, long RunSeed, long MaximumStartedRequests, long MaximumPostRequests, string ScenarioMixContract, IReadOnlySet<string> ScenarioNames)
{
    internal static ApiSoakExecutionContract Parse(JsonElement value, string name)
    {
        string mode = ApiSoakArtifact.RequiredString(value, "LoadMode", name);
        if (mode is not "ClosedLoop" and not "ConstantArrivalRate")
        {
            throw ApiSoakArtifact.Invalid(name, "LoadMode is unsupported.");
        }
        if (!value.TryGetProperty(nameof(ArrivalRatePerSecond), out JsonElement arrival))
        {
            throw ApiSoakArtifact.Invalid(name, "ArrivalRatePerSecond is required.");
        }
        double? arrivalRate = arrival.ValueKind == JsonValueKind.Null ? null : ApiSoakArtifact.RequiredPositiveDouble(value, "ArrivalRatePerSecond", name);
        if (mode == "ClosedLoop" && arrivalRate is not null || mode == "ConstantArrivalRate" && arrivalRate is null)
        {
            throw ApiSoakArtifact.Invalid(name, "LoadMode and ArrivalRatePerSecond are inconsistent.");
        }
        if (!value.TryGetProperty("ScenarioMix", out JsonElement mix) || mix.ValueKind != JsonValueKind.Array || mix.GetArrayLength() == 0)
        {
            throw ApiSoakArtifact.Invalid(name, "ScenarioMix must be a non-empty array.");
        }
        var entries = new List<string>();
        var scenarioNames = new HashSet<string>(StringComparer.Ordinal);
        long totalWeight = 0;
        foreach (JsonElement entry in mix.EnumerateArray())
        {
            string scenario = ApiSoakArtifact.RequiredString(entry, "Scenario", name);
            long weight = ApiSoakArtifact.RequiredPositiveLong(entry, "Weight", name);
            if (!Enum.TryParse<ApiLoadTestScenario>(scenario, false, out _) || scenario == nameof(ApiLoadTestScenario.Login) || weight > 100 || !scenarioNames.Add(scenario))
            {
                throw ApiSoakArtifact.Invalid(name, "ScenarioMix contains an unknown, duplicate, or out-of-range entry.");
            }
            totalWeight += weight;
            entries.Add($"{scenario}:{weight}");
        }
        if (totalWeight != 100)
        {
            throw ApiSoakArtifact.Invalid(name, "ScenarioMix weights must sum to one hundred.");
        }
        entries.Sort(StringComparer.Ordinal);
        return new(ApiSoakArtifact.RequiredString(value, "SuiteProfile", name), ApiSoakArtifact.RequiredPositiveDouble(value, "WarmupSeconds", name), ApiSoakArtifact.RequiredPositiveDouble(value, "MeasurementSeconds", name), ApiSoakArtifact.RequiredPositiveLong(value, "ClientConcurrency", name), mode, arrivalRate, ApiSoakArtifact.RequiredPositiveLong(value, "RunSeed", name), ApiSoakArtifact.RequiredPositiveLong(value, "MaximumStartedRequests", name), ApiSoakArtifact.RequiredPositiveLong(value, "MaximumPostRequests", name), string.Join('|', entries), scenarioNames);
    }
}

internal sealed record ApiSoakDatasetContract(string Profile, long Seed) { internal static ApiSoakDatasetContract Parse(JsonElement value, string name) => new(ApiSoakArtifact.RequiredString(value, "Profile", name), ApiSoakArtifact.RequiredPositiveLong(value, "Seed", name)); }
internal sealed record ApiSoakEnvironmentContract(string Runtime, string OperatingSystem, string Transport, string Tls) { internal static ApiSoakEnvironmentContract Parse(JsonElement value, string name) => new(ApiSoakArtifact.RequiredString(value, "Runtime", name), ApiSoakArtifact.RequiredString(value, "OS", name), ApiSoakArtifact.RequiredString(value, "Transport", name), ApiSoakArtifact.RequiredString(value, "Tls", name)); }
internal sealed record ApiSoakHttpMetrics(long Completed, long Successful, double P50Ms, double P95Ms, double P99Ms, double ThroughputRequestsPerSecond, long UnexpectedHttp, long RateLimited, long TimeoutOrCancellation, long TransportFailures) { internal bool HasFailure => UnexpectedHttp != 0 || RateLimited != 0 || TimeoutOrCancellation != 0 || TransportFailures != 0; internal static ApiSoakHttpMetrics Parse(JsonElement value, string name) => new(ApiSoakArtifact.RequiredLong(value, "Completed", name), ApiSoakArtifact.RequiredLong(value, "Successful", name), ApiSoakArtifact.RequiredDouble(value, "ApproximateP50Ms", name), ApiSoakArtifact.RequiredDouble(value, "ApproximateP95Ms", name), ApiSoakArtifact.RequiredDouble(value, "ApproximateP99Ms", name), ApiSoakArtifact.RequiredDouble(value, "ThroughputRequestsPerSecond", name), ApiSoakArtifact.RequiredLong(value, "UnexpectedHttp", name), ApiSoakArtifact.RequiredLong(value, "RateLimited", name), ApiSoakArtifact.RequiredLong(value, "TimeoutOrCancellation", name), ApiSoakArtifact.RequiredLong(value, "TransportFailures", name)); }
internal sealed record ApiSoakMemoryObservation(long ObservedEndMinusStartBytes, long PeakBytes) { internal static ApiSoakMemoryObservation Parse(JsonElement value, string name) => new(ApiSoakArtifact.RequiredSignedLong(value, "ObservedEndMinusStartBytes", name), ApiSoakArtifact.RequiredLong(value, "PeakBytes", name)); }
internal sealed record ApiSoakPoolEvidence(bool ConnectionCountAvailable, bool ConnectionMaxAvailable, bool ConnectionTimeoutsAvailable, long UsedConnections, long MaxConnections, long PoolTimeouts) { internal bool IsFullyAvailable => ConnectionCountAvailable && ConnectionMaxAvailable && ConnectionTimeoutsAvailable; internal static ApiSoakPoolEvidence Parse(JsonElement value, string name) => new(ApiSoakArtifact.RequiredBoolean(value, "ConnectionCountAvailable", name), ApiSoakArtifact.RequiredBoolean(value, "ConnectionMaxAvailable", name), ApiSoakArtifact.RequiredBoolean(value, "ConnectionTimeoutsAvailable", name), ApiSoakArtifact.RequiredLong(value, "UsedConnections", name), ApiSoakArtifact.RequiredPositiveLong(value, "MaxConnections", name), ApiSoakArtifact.RequiredLong(value, "PoolTimeouts", name)); }
internal sealed record ApiSoakLockEvidence(bool IsAvailable, long FailureCount, double SamplingIntervalMilliseconds, long SampleCount, long SamplesWithLockWaits, long TotalWaitingSessionObservations, long MaxConcurrentWaitingSessions, double ApproximateObservedWaitingSessionMilliseconds) { internal static ApiSoakLockEvidence Parse(JsonElement value, string name) => new(ApiSoakArtifact.RequiredBoolean(value, "IsAvailable", name), ApiSoakArtifact.RequiredLong(value, "FailureCount", name), ApiSoakArtifact.RequiredPositiveDouble(value, "SamplingIntervalMilliseconds", name), ApiSoakArtifact.RequiredLong(value, "SampleCount", name), ApiSoakArtifact.RequiredLong(value, "SamplesWithLockWaits", name), ApiSoakArtifact.RequiredLong(value, "TotalWaitingSessionObservations", name), ApiSoakArtifact.RequiredLong(value, "MaxConcurrentWaitingSessions", name), ApiSoakArtifact.RequiredDouble(value, "ApproximateObservedWaitingSessionMilliseconds", name)); }
internal sealed record ApiSoakRecoveryEvidence(string Status, bool Ready, bool RepresentativeRead) { internal bool IsPassed => Status == "passed" && Ready && RepresentativeRead; internal static ApiSoakRecoveryEvidence Parse(JsonElement value, string name) => new(ApiSoakArtifact.RequiredString(value, "Status", name), ApiSoakArtifact.RequiredBoolean(value, "Ready", name), ApiSoakArtifact.RequiredBoolean(value, "RepresentativeRead", name)); }
