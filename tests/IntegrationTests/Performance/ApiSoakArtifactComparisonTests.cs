using System.Text.Json;

namespace IntegrationTests.Performance;

public sealed class ApiSoakArtifactComparisonTests
{
    [Fact]
    public void Compare_EquivalentArtifacts_SurfacesOnlyConfiguredRegressions()
    {
        string baseline = CreateArtifact();
        string candidate = CreateArtifact(p50: 12, p95: 11, p99: 11, throughput: 110);

        ApiSoakArtifactComparisonResult result = ApiSoakArtifactComparison.Compare(baseline, candidate,
            new ApiSoakComparisonThresholds(10, 10));

        result.Outcome.ShouldBe("regression_detected");
        result.Metrics.Single(metric => metric.Name == "http_p50_ms").ShouldSatisfyAllConditions(
            metric => metric.IsRegression.ShouldBeTrue(),
            metric => metric.PercentChange.ShouldBe(20));
        result.Metrics.Single(metric => metric.Name == "successful_throughput_requests_per_second").IsRegression.ShouldBeFalse();
        result.Metrics.Single(metric => metric.Name == "managed_memory_peak_bytes").Assessment.ShouldBe("observational");
    }

    [Fact]
    public void Compare_ZeroBaseline_DoesNotInventPercentChange()
    {
        ApiSoakArtifactComparisonResult result = ApiSoakArtifactComparison.Compare(CreateArtifact(p50: 0), CreateArtifact(p50: 5));

        ApiSoakComparisonMetric p50 = result.Metrics.Single(metric => metric.Name == "http_p50_ms");
        p50.PercentChange.ShouldBeNull();
        p50.IsRegression.ShouldBeFalse();
    }

    [Fact]
    public void Compare_LegacyArtifactWithoutContract_FailsClosed()
    {
        string legacy = CreateArtifact().Replace("\"SchemaVersion\":\"api_soak_artifact_v2\",", string.Empty, StringComparison.Ordinal);

        ArgumentException exception = Should.Throw<ArgumentException>(() => ApiSoakArtifactComparison.Compare(legacy, CreateArtifact()));

        exception.Message.ShouldContain("SchemaVersion");
    }

    [Fact]
    public void Compare_DifferentLoadContracts_FailsClosed()
    {
        string candidate = CreateArtifact(loadMode: "ConstantArrivalRate", arrivalRate: 10);

        ArgumentException exception = Should.Throw<ArgumentException>(() => ApiSoakArtifactComparison.Compare(CreateArtifact(), candidate));

        exception.Message.ShouldContain("load mode");
    }

    [Fact]
    public void Compare_ReorderedWorkerProperties_RemainsComparable()
    {
        string baseline = CreateArtifact();
        string candidate = baseline.Replace("\"WorkerProfile\":{\"Profile\":\"IsolatedRequestCost\",\"BackgroundWorkers\":\"removed\"}",
            "\"WorkerProfile\":{\"BackgroundWorkers\":\"removed\",\"Profile\":\"IsolatedRequestCost\"}", StringComparison.Ordinal);

        ApiSoakArtifactComparison.Compare(baseline, candidate).Outcome.ShouldBe("no_threshold_regression_detected");
    }

    [Theory]
    [InlineData("\"Profile\":\"Small\"", "\"Profile\":\"Medium\"", "dataset profile")]
    [InlineData("\"Runtime\":\".NET 10\"", "\"Runtime\":\".NET 11\"", "runtime")]
    [InlineData("\"RunSeed\":20260911", "\"RunSeed\":20260912", "run seed")]
    public void Compare_MismatchedExecutionEnvironmentOrDataset_FailsClosed(string source, string replacement, string expected)
    {
        string candidate = CreateArtifact().Replace(source, replacement, StringComparison.Ordinal);

        ArgumentException exception = Should.Throw<ArgumentException>(() => ApiSoakArtifactComparison.Compare(CreateArtifact(), candidate));

        exception.Message.ShouldContain(expected);
    }

    [Fact]
    public void Compare_MalformedArtifact_FailsClosed()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => ApiSoakArtifactComparison.Compare("{", CreateArtifact()));

        exception.Message.ShouldContain("JSON is malformed");
    }

    [Theory]
    [InlineData("\"PoolTimeouts\":0", "\"PoolTimeouts\":1", "pool evidence")]
    [InlineData("\"LoadMode\":\"ClosedLoop\"", "\"LoadMode\":\"Unsupported\"", "LoadMode")]
    public void Compare_InvalidPassedEvidence_FailsClosed(string source, string replacement, string expected)
    {
        string invalid = CreateArtifact().Replace(source, replacement, StringComparison.Ordinal);

        ArgumentException exception = Should.Throw<ArgumentException>(() => ApiSoakArtifactComparison.Compare(invalid, CreateArtifact()));

        exception.Message.ShouldContain(expected);
    }

    [Fact]
    public void Compare_FailedLockSamplerEvidence_FailsClosed()
    {
        string invalid = CreateArtifact().Replace("\"PostgreSqlSampledLockWaitOccupancy\":{\"IsAvailable\":true,\"FailureCount\":0",
            "\"PostgreSqlSampledLockWaitOccupancy\":{\"IsAvailable\":true,\"FailureCount\":1", StringComparison.Ordinal);

        ArgumentException exception = Should.Throw<ArgumentException>(() => ApiSoakArtifactComparison.Compare(invalid, CreateArtifact()));

        exception.Message.ShouldContain("lock-wait evidence");
    }

    [Fact]
    public void Compare_InvalidRecoveryPoolEvidence_FailsClosed()
    {
        string invalid = CreateArtifact().Replace("\"RecoveryNpgsqlPool\":{\"ConnectionCountAvailable\":true,\"ConnectionMaxAvailable\":true,\"ConnectionTimeoutsAvailable\":true,\"UsedConnections\":0,\"MaxConnections\":10,\"PoolTimeouts\":0}",
            "\"RecoveryNpgsqlPool\":{\"ConnectionCountAvailable\":true,\"ConnectionMaxAvailable\":true,\"ConnectionTimeoutsAvailable\":true,\"UsedConnections\":0,\"MaxConnections\":10,\"PoolTimeouts\":1}", StringComparison.Ordinal);

        ArgumentException exception = Should.Throw<ArgumentException>(() => ApiSoakArtifactComparison.Compare(invalid, CreateArtifact()));

        exception.Message.ShouldContain("pool evidence");
    }

    [Fact]
    public async Task WriteValidatedAsync_WritesAtomicSanitizedAggregateOnlyResult()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eiams-api-soak-results", Guid.NewGuid().ToString("N"));
        try
        {
            ApiSoakArtifactComparisonResult result = ApiSoakArtifactComparison.Compare(CreateArtifact(), CreateArtifact());
            string path = await ApiSoakArtifactComparison.WriteValidatedAsync(directory, "comparison.json", result);

            string json = await File.ReadAllTextAsync(path);
            ApiSoakArtifactSafetyValidator.EnsureSafe(json);
            json.ShouldContain("api_soak_comparison_v1");
            json.ShouldNotContain(directory);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string CreateArtifact(double p50 = 10, double p95 = 10, double p99 = 10,
        double throughput = 100, string loadMode = "ClosedLoop", double? arrivalRate = null)
    {
        var artifact = new
        {
            SchemaVersion = ApiSoakArtifactComparisonSchema.ArtifactVersion,
            Status = "passed",
            ExecutionProfile = new
            {
                SuiteProfile = "QuickValidation",
                WarmupSeconds = 1d,
                MeasurementSeconds = 12d,
                ClientConcurrency = 2,
                LoadMode = loadMode,
                ArrivalRatePerSecond = arrivalRate,
                RunSeed = 20260911,
                MaximumStartedRequests = 10000,
                MaximumPostRequests = 1000,
                ScenarioMix = new[] { new { Scenario = "ReadList", Weight = 90 }, new { Scenario = "Post", Weight = 10 } }
            },
            Dataset = new { Profile = "Small", Seed = 20260911 },
            WorkerProfile = new { Profile = "IsolatedRequestCost", BackgroundWorkers = "removed" },
            Environment = new { Runtime = ".NET 10", OS = "Linux", Transport = "Kestrel loopback HTTP", Tls = "not_used" },
            Http = new
            {
                Completed = 100,
                Successful = 100,
                ApproximateP50Ms = p50,
                ApproximateP95Ms = p95,
                ApproximateP99Ms = p99,
                ThroughputRequestsPerSecond = throughput,
                UnexpectedHttp = 0,
                RateLimited = 0,
                TimeoutOrCancellation = 0,
                TransportFailures = 0
            },
            ScenarioMetrics = new
            {
                ReadList = new { Count = 90, SuccessCount = 90, FailureCount = 0 },
                Post = new { Count = 10, SuccessCount = 10, FailureCount = 0 }
            },
            ManagedMemory = new { ObservedEndMinusStartBytes = 10L, PeakBytes = 100L },
            NpgsqlPool = new { ConnectionCountAvailable = true, ConnectionMaxAvailable = true, ConnectionTimeoutsAvailable = true, UsedConnections = 0L, MaxConnections = 10L, PoolTimeouts = 0L },
            RecoveryNpgsqlPool = new { ConnectionCountAvailable = true, ConnectionMaxAvailable = true, ConnectionTimeoutsAvailable = true, UsedConnections = 0L, MaxConnections = 10L, PoolTimeouts = 0L },
            PostgreSqlSampledLockWaitOccupancy = new
            {
                IsAvailable = true,
                FailureCount = 0L,
                SamplingIntervalMilliseconds = 25d,
                SampleCount = 1L,
                SamplesWithLockWaits = 0L,
                TotalWaitingSessionObservations = 0L,
                MaxConcurrentWaitingSessions = 0L,
                ApproximateObservedWaitingSessionMilliseconds = 0d
            },
            Recovery = new { Status = "passed", Ready = true, RepresentativeRead = true }
        };
        return JsonSerializer.Serialize(artifact);
    }
}
