using System.Text.Json;

namespace IntegrationTests.Performance;

public sealed class ApiLoadSuiteConfigurationTests
{
    [Fact]
    public void FromEnvironment_QuickValidation_DefaultsToSmallAndBothSafeModes()
    {
        var values = new Dictionary<string, string?> { ["RUN_API_LOAD_SUITE"] = "1" };

        var suite = ApiLoadSuiteConfiguration.FromEnvironment(values.GetValueOrDefault);

        suite.Profile.ShouldBe(ApiLoadSuiteProfile.QuickValidation);
        suite.DatasetProfile.ShouldBe(DatasetProfile.Small);
        suite.Plan.Runs.Count.ShouldBe(4);
        suite.Plan.Runs.Select(run => run.Mode).Distinct().ShouldBe([ApiLoadTestMode.ClosedLoop, ApiLoadTestMode.ConstantArrivalRate]);
        suite.Plan.Runs.ShouldAllBe(run => run.Measurement.Duration == TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void FromEnvironment_RejectsMissingGateFullWithoutOptInAndUnsafeQuickDataset()
    {
        Should.Throw<InvalidOperationException>(() => ApiLoadSuiteConfiguration.FromEnvironment(_ => null));
        Should.Throw<InvalidOperationException>(() => ApiLoadSuiteConfiguration.FromEnvironment(name => name switch
        {
            "RUN_API_LOAD_SUITE" => "1", "EIAMS_API_LOAD_PROFILE" => "FullBaseline", _ => null
        }));
        Should.Throw<InvalidOperationException>(() => ApiLoadSuiteConfiguration.FromEnvironment(name => name switch
        {
            "RUN_API_LOAD_SUITE" => "1", "EIAMS_API_LOAD_DATASET" => "Medium", _ => null
        }));
    }

    [Fact]
    public void RunNamespace_IsShortAllowedAndRandomized()
    {
        var run = new ApiLoadTestRunDefinition(3, 50, ApiLoadTestMode.ConstantArrivalRate, 1, 1,
            new ApiLoadTestPhase(ApiLoadTestPhaseKind.Warmup, TimeSpan.FromSeconds(1)),
            new ApiLoadTestPhase(ApiLoadTestPhaseKind.Measurement, TimeSpan.FromSeconds(1)));

        string first = ApiLoadSuiteConfiguration.CreateRunNamespace(run);
        string second = ApiLoadSuiteConfiguration.CreateRunNamespace(run);

        first.Length.ShouldBeLessThanOrEqualTo(24);
        first.ShouldMatch("^[a-z0-9-]+$");
        first.ShouldNotBe(second);
        first.ShouldContain("r3-a50-");
    }

    [Fact]
    public void FromEnvironment_ResultDirectory_RequiresActualChildContainment()
    {
        string root = Path.Combine(Path.GetTempPath(), "eiams-api-load-results");
        string child = Path.Combine(root, "child");
        string sibling = root + "-evil";
        var accepted = ApiLoadSuiteConfiguration.FromEnvironment(name => name switch
        {
            "RUN_API_LOAD_SUITE" => "1", "EIAMS_API_LOAD_RESULT_DIR" => child, _ => null
        });

        accepted.ResultDirectory.ShouldBe(Path.GetFullPath(child));
        Should.Throw<ArgumentException>(() => ApiLoadSuiteConfiguration.FromEnvironment(name => name switch
        {
            "RUN_API_LOAD_SUITE" => "1", "EIAMS_API_LOAD_RESULT_DIR" => sibling, _ => null
        }));
    }

    [Fact]
    public async Task ArtifactWriter_WritesAtomicallyWithSafeNameAndNoCredentialFields()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eiams-api-load-results", $"test-{Guid.NewGuid():N}");
        try
        {
            string path = await ApiLoadSuiteArtifactWriter.WriteAsync(directory, "safe.json", new { Status = "passed", Transport = "loopback" });
            File.Exists(path).ShouldBeTrue();
            Directory.GetFiles(directory, "*.tmp").ShouldBeEmpty();
            string json = await File.ReadAllTextAsync(path);
            JsonDocument.Parse(json).RootElement.TryGetProperty("password", out _).ShouldBeFalse();
            json.Contains("connection", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
            Should.Throw<ArgumentException>(() => ApiLoadSuiteArtifactWriter.WriteAsync(directory, "../unsafe.json", new { }).GetAwaiter().GetResult());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Executor_RequestSafetyCap_MarksPhaseIncompleteRatherThanSuccessful()
    {
        var clock = new SuiteClock();
        var executor = new ApiLoadTestExecutor(clock, (_, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            return Task.FromResult(new ApiLoadTestExecutionSample(true));
        }, new ApiLoadTestExecutorOptions(TimeSpan.FromSeconds(1), MaximumStartedRequests: 2));
        var run = new ApiLoadTestRunDefinition(1, 1, ApiLoadTestMode.ClosedLoop, null, 1,
            new ApiLoadTestPhase(ApiLoadTestPhaseKind.Warmup, TimeSpan.Zero),
            new ApiLoadTestPhase(ApiLoadTestPhaseKind.Measurement, TimeSpan.FromSeconds(5)));

        ApiLoadTestExecutionResult result = await executor.ExecuteAsync(run);

        result.Measurement.StartedCount.ShouldBe(2);
        result.Measurement.SafetyCapExceeded.ShouldBeTrue();
        result.Measurement.CompletedCount.ShouldBe(2);
    }

    [Fact]
    public void Acceptance_RejectsCancellationAndShutdown()
    {
        ApiLoadTestPhaseExecutionSummary phase = CreateCompletePhase() with { CancellationRequested = true };
        var cancelled = new ApiLoadTestExecutionResult(phase, CreateCompletePhase(), false, false);
        var shutdown = new ApiLoadTestExecutionResult(CreateCompletePhase(), CreateCompletePhase(), false, true);

        Should.Throw<ShouldAssertException>(() => ApiLoadSuiteAcceptance.EnsureExecutionComplete(cancelled));
        Should.Throw<ShouldAssertException>(() => ApiLoadSuiteAcceptance.EnsureExecutionComplete(shutdown));
    }

    private static ApiLoadTestPhaseExecutionSummary CreateCompletePhase() => new(1, 1, 1, 0, 0, 0, 1, 0, 0, false, 0);

    private sealed class SuiteClock : IApiLoadTestClock
    {
        public TimeSpan Elapsed { get; private set; }

        public Task DelayUntilAsync(TimeSpan deadline, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Elapsed = TimeSpan.FromTicks(Math.Max(Elapsed.Ticks, deadline.Ticks));
            return Task.CompletedTask;
        }

        internal void Advance(TimeSpan duration) => Elapsed += duration;
    }
}
