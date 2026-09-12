namespace IntegrationTests.Performance;

internal static class BenchmarkPhaseMetadata
{
    internal static IReadOnlyList<BenchmarkPhaseResult> Create(
        double hostStartupAndJitMs,
        double firstDatabaseHealthProbeMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hostStartupAndJitMs);
        ArgumentOutOfRangeException.ThrowIfNegative(firstDatabaseHealthProbeMs);

        return
        [
            new("host_start_and_jit", "measured", hostStartupAndJitMs,
                "Starts a new in-process Kestrel host; excludes operating-system process start."),
            new("first_database_health_probe", "measured", firstDatabaseHealthProbeMs,
                "First readiness probe from the benchmark host; not evidence of a database wake-up."),
            new("cold_process", "not_measured", null,
                "The benchmark runs inside an existing test process, so operating-system process start is not measured."),
            new("database_wake_up", "not_measured", null,
                "Requires a controlled database restart or database-level evidence; no API request is labeled cold DB."),
            new("warm_api_cache", "not_verified", null,
                "Endpoint warm-up requests are executed before samples, but cache residency is not inferred from latency."),
            new("warm_database_buffers", "not_verified", null,
                "Database buffer residency is not inferred from API latency and requires database-level instrumentation.")
        ];
    }
}

internal sealed record BenchmarkPhaseResult(
    string Name,
    string State,
    double? DurationMs,
    string Interpretation);
