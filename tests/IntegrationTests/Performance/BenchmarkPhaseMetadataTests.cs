namespace IntegrationTests.Performance;

public sealed class BenchmarkPhaseMetadataTests
{
    [Fact]
    public void Create_ShouldSeparateMeasuredHostAndDatabaseProbeFromUnverifiedColdStates()
    {
        // Act
        IReadOnlyList<BenchmarkPhaseResult> phases = BenchmarkPhaseMetadata.Create(12.5, 7.25);

        // Assert
        phases[0].ShouldBe(new BenchmarkPhaseResult(
            "host_start_and_jit", "measured", 12.5,
            "Starts a new in-process Kestrel host; excludes operating-system process start."));
        phases[1].ShouldBe(new BenchmarkPhaseResult(
            "first_database_health_probe", "measured", 7.25,
            "First readiness probe from the benchmark host; not evidence of a database wake-up."));
        phases.Skip(2).Select(phase => phase.State).ShouldBe(
            ["not_measured", "not_measured", "not_verified", "not_verified"]);
        phases.Select(phase => phase.Interpretation).ShouldAllBe(text =>
            !text.Contains("cold DB", StringComparison.Ordinal) ||
            text.Contains("no API request is labeled cold DB", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_ShouldRejectNegativeMeasuredDurations()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => BenchmarkPhaseMetadata.Create(-1, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => BenchmarkPhaseMetadata.Create(0, -1));
    }
}
