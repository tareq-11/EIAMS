using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ApiLoadTestSmokeTests(
    IntegrationTestWebAppFactory factory,
    ITestOutputHelper output)
{
    [ExplicitApiLoadTestFact]
    [Trait("Category", "Performance")]
    [Trait("WorkloadClass", PerformanceWorkloadContracts.NormalExpectedTraffic)]
    public async Task RunLocalKestrelSmokeWithFullMixedWorkloadAsync()
    {
        const long seed = 20260908;
        string databaseName = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString).Database
            ?? throw new InvalidOperationException("Integration test database name is required for synthetic seeding.");
        await SyntheticDatasetSeeder.SeedAsync(
            DatasetProfile.Small,
            new SyntheticDatasetSeedOptions(factory.DatabaseConnectionString, databaseName, "Test", seed));
        SyntheticDatasetManifest manifest = SyntheticDatasetManifestFactory.Create(DatasetProfile.Small, seed);
        var fixture = new ApiLoadTestFixtureContext(
            manifest.GetOrganizationId(0),
            // Isolates repeated explicit smoke invocations against the same Testcontainer database.
            // It is deliberately never included in output labels or result JSON.
            $"smoke-{Guid.NewGuid():N}"[..22],
            MaximumPostRequests: 100);
        SqlCommandCounterInterceptor commandCollector = factory.Services.GetRequiredService<SqlCommandCounterInterceptor>();
        using var poolCollector = new NpgsqlPoolStateCollector();
        await using var lockWaitSampler = new PostgreSqlLockWaitSampler(factory.DatabaseConnectionString);
        ApiBenchmarkWorkerProfile workerProfile = ApiBenchmarkWorkerProfiles.GetRequestedProfile();
        using IntegrationTestWebAppFactory.BenchmarkProfiledWebAppFactory smokeFactory =
            factory.CreateSiblingFactory(commandCollector, workerProfile);
        smokeFactory.UseKestrel(0);
        using HttpClient client = smokeFactory.CreateClient();
        client.BaseAddress = new Uri(client.BaseAddress!, "api/v1/");
        using var adapter = new ApiLoadTestHttpAdapter(
            client,
            IntegrationTestWebAppFactory.AdministratorEmail,
            IntegrationTestWebAppFactory.AdministratorPassword,
            fixture);
        await adapter.AuthenticateAsync(CancellationToken.None);
        var run = new ApiLoadTestRunDefinition(
            1, 1, ApiLoadTestMode.ClosedLoop, null, 1,
            new ApiLoadTestPhase(ApiLoadTestPhaseKind.Warmup, TimeSpan.FromSeconds(1)),
            new ApiLoadTestPhase(ApiLoadTestPhaseKind.Measurement, TimeSpan.FromSeconds(2)),
            ApiLoadTestScenarioMix.Weights);
        var executor = new ApiLoadTestExecutor(new StopwatchApiLoadTestClock(), adapter.ExecuteAsync);
        SqlCommandDurationSnapshot? warmupSql = null;
        SqlCommandDurationSnapshot? measurementSql = null;
        NpgsqlPoolStateSnapshot? warmupPoolState = null;
        NpgsqlPoolStateSnapshot? measurementPoolState = null;
        PostgreSqlLockWaitSnapshot? warmupLockWait = null;
        PostgreSqlLockWaitSnapshot? measurementLockWait = null;
        ApiLoadTestExecutionResult execution = await executor.ExecuteAsync(
            run,
            beforePhaseAsync: (_, cancellationToken) =>
            {
                commandCollector.Reset();
                poolCollector.Reset();
                return lockWaitSampler.StartAsync(cancellationToken);
            },
            afterPhaseAsync: async (phase, _) =>
            {
                PostgreSqlLockWaitSnapshot snapshot = await lockWaitSampler.StopAsync();
                if (phase == ApiLoadTestPhaseKind.Warmup)
                {
                    warmupSql = commandCollector.Snapshot();
                    warmupPoolState = poolCollector.Snapshot();
                    warmupLockWait = snapshot;
                }
                else
                {
                    measurementSql = commandCollector.Snapshot();
                    measurementPoolState = poolCollector.Snapshot();
                    measurementLockWait = snapshot;
                }
            });

        ApiLoadTestHttpMetrics metrics = adapter.GetMetrics();
        IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics> scenarioMetrics = adapter.GetScenarioMetrics();
        var result = new
        {
            WorkloadClass = PerformanceWorkloadContracts.NormalExpectedTraffic,
            Transport = "Kestrel loopback HTTP",
            Database = "integration-testcontainer-local",
            WorkerProfile = smokeFactory.WorkerProfileMetadata,
            Dataset = ApiBenchmarkWorkerProfiles.CreateLoadSmokeDatasetMetadata(seed),
            Scenarios = new[] { "login", "read-list", "read-detail", "report", "post" },
            Metrics = metrics,
            ScenarioMetrics = scenarioMetrics,
            Execution = execution,
            SqlCommandDurationByPhase = new { Warmup = warmupSql, Measurement = measurementSql },
            NpgsqlPoolStateByPhase = new { Warmup = warmupPoolState, Measurement = measurementPoolState },
            PostgreSqlSampledLockWaitOccupancyByPhase = new { Warmup = warmupLockWait, Measurement = measurementLockWait },
            MeasurementMetadata = new
            {
                SqlCommandDuration = "EF/Npgsql DbCommandInterceptor execution duration; bounded logarithmic histogram p50/p95/p99 is an approximate upper bound. Authentication, migrations, and seeding are excluded by reset at phase boundaries.",
                NpgsqlPoolWait = "not_available: Npgsql 10.0.3 Meter exposes pool state/timeouts but no connection-acquisition wait duration.",
                NpgsqlPoolState = "Npgsql Meter process-wide aggregate across all pools per snapshot: db.client.connection.count state=idle|used, db.client.connection.max, and phase timeouts. Provider tags are discarded; this is neither endpoint/pool attribution nor pool-wait duration.",
                PostgreSqlSampledLockWaitOccupancy = "A separate Pooling=false monitor runs one pg_stat_activity query per configured interval, scoped to the current database and excluding its PID; this adds monitoring overhead. sampleCount is polling observations; samplesWithLockWaits counts observations with one or more sessions where wait_event_type=Lock; totalWaitingSessionObservations sums waiting sessions across samples; maxConcurrentWaitingSessions is the observed maximum. approximateObservedWaitingSessionMilliseconds uses monotonic elapsed time between observations with a left-endpoint occupancy assumption and saturates at a bounded maximum. It is a sampled estimate, not exact lock-wait duration, per-request attribution, or a complete census between samples. No connection details, PIDs, query text, SQL, or workload identifiers are emitted.",
                RawSql = "not_measured"
            }
        };
        string resultPath = Path.Combine(
            Path.GetTempPath(),
            $"eiams-api-load-smoke-{smokeFactory.WorkerProfileMetadata.Profile}-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result));
        output.WriteLine($"JSON result: {resultPath}");

        PerformanceWorkloadContracts.EnsureNormalExpectedTrafficHasNoFailures(metrics);
        metrics.Successful.ShouldBeGreaterThan(0);
        execution.Warmup.FaultedCount.ShouldBe(0);
        execution.Measurement.FaultedCount.ShouldBe(0);
        execution.Warmup.CancelledCount.ShouldBe(0);
        execution.Measurement.CancelledCount.ShouldBe(0);
        execution.Measurement.DroppedSaturationCount.ShouldBe(0);
        execution.Measurement.InFlightAtSummaryCount.ShouldBe(0);
        execution.Measurement.UnfinishedAfterGraceCount.ShouldBe(0);
        execution.Measurement.CompletedCount.ShouldBeGreaterThan(0);
        execution.Measurement.SuccessfulCount.ShouldBeGreaterThan(0);
        foreach (ApiLoadTestScenario scenario in ApiLoadTestScenarioMix.Weights.Select(weight => weight.Scenario))
        {
            scenarioMetrics[scenario].Count.ShouldBeGreaterThan(0);
            scenarioMetrics[scenario].FailureCount.ShouldBe(0);
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitApiLoadTestFactAttribute : FactAttribute
{
    public ExplicitApiLoadTestFactAttribute()
    {
        if (!ApiLoadTestFeatureGate.IsEnabled)
        {
            Skip = "Explicit local Kestrel/Testcontainers API load smoke. Set RUN_API_LOAD_TESTS=1 to run it.";
        }
    }
}
