using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ApiLoadTestSmokeTests(IntegrationTestWebAppFactory factory, ITestOutputHelper output)
{
    [ExplicitApiLoadTestFact]
    [Trait("Category", "Performance")]
    [Trait("WorkloadClass", PerformanceWorkloadContracts.NormalExpectedTraffic)]
    public async Task RunExplicitConfigurableApiLoadSuiteAsync()
    {
        var suite = ApiLoadSuiteConfiguration.FromEnvironment();
        string databaseName = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString).Database
            ?? throw new InvalidOperationException("Integration test database name is required for synthetic seeding.");
        await SyntheticDatasetSeeder.SeedAsync(suite.DatasetProfile,
            new SyntheticDatasetSeedOptions(factory.DatabaseConnectionString, databaseName, "Test", suite.Seed));
        SyntheticDatasetManifest manifest = SyntheticDatasetManifestFactory.Create(suite.DatasetProfile, suite.Seed);

        foreach (ApiLoadTestRunDefinition run in suite.Plan.Runs)
        {
            await ExecuteRunAsync(suite, manifest, run);
        }
    }

    private async Task ExecuteRunAsync(ApiLoadSuiteConfiguration suite, SyntheticDatasetManifest manifest, ApiLoadTestRunDefinition run)
    {
        // Each run owns its adapter, token, namespace, host, collectors, and monitor lifecycle.
        SqlCommandCounterInterceptor commandCollector = factory.Services.GetRequiredService<SqlCommandCounterInterceptor>();
        commandCollector.Reset();
        string runNamespace = ApiLoadSuiteConfiguration.CreateRunNamespace(run);
        var fixture = new ApiLoadTestFixtureContext(manifest.GetOrganizationId(0), runNamespace, suite.MaximumPostRequestsPerRun);
        ApiLoadTestHttpMetrics? warmupMetrics = null;
        ApiLoadTestHttpMetrics? measurementMetrics = null;
        IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics>? warmupScenarios = null;
        IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics>? measurementScenarios = null;
        ApiLoadTestExecutionResult? execution = null;
        SqlCommandDurationSnapshot? warmupSql = null;
        SqlCommandDurationSnapshot? measurementSql = null;
        NpgsqlPoolStateSnapshot? warmupPool = null;
        NpgsqlPoolStateSnapshot? measurementPool = null;
        PostgreSqlLockWaitSnapshot? warmupLocks = null;
        PostgreSqlLockWaitSnapshot? measurementLocks = null;
        Exception? failure = null;

        try
        {
            using var poolCollector = new NpgsqlPoolStateCollector();
            await using var lockWaitSampler = new PostgreSqlLockWaitSampler(factory.DatabaseConnectionString);
            using IntegrationTestWebAppFactory.BenchmarkProfiledWebAppFactory runFactory =
                factory.CreateSiblingFactory(commandCollector, suite.WorkerProfile);
            runFactory.UseKestrel(0);
            using HttpClient client = runFactory.CreateClient();
            client.BaseAddress = new Uri(client.BaseAddress!, "api/v1/");
            using var adapter = new ApiLoadTestHttpAdapter(client, IntegrationTestWebAppFactory.AdministratorEmail,
                IntegrationTestWebAppFactory.AdministratorPassword, fixture);
            await adapter.AuthenticateAsync(CancellationToken.None); // setup excluded from phase metrics
            var executor = new ApiLoadTestExecutor(new StopwatchApiLoadTestClock(), adapter.ExecuteAsync,
                new ApiLoadTestExecutorOptions(TimeSpan.FromSeconds(10), suite.MaximumStartedRequestsPerPhase));
            execution = await executor.ExecuteAsync(run,
                async (phase, token) =>
                {
                    commandCollector.Reset(); poolCollector.Reset(); adapter.ResetMetrics();
                    await lockWaitSampler.StartAsync(token);
                },
                async (phase, _) =>
                {
                    PostgreSqlLockWaitSnapshot locks = await lockWaitSampler.StopAsync();
                    if (phase == ApiLoadTestPhaseKind.Warmup)
                    {
                        warmupSql = commandCollector.Snapshot(); warmupPool = poolCollector.Snapshot(); warmupLocks = locks;
                        warmupMetrics = adapter.GetMetrics(); warmupScenarios = adapter.GetScenarioMetrics();
                    }
                    else
                    {
                        measurementSql = commandCollector.Snapshot(); measurementPool = poolCollector.Snapshot(); measurementLocks = locks;
                        measurementMetrics = adapter.GetMetrics(); measurementScenarios = adapter.GetScenarioMetrics();
                    }
                });
            EnsureSuccessfulRun(warmupMetrics!, warmupScenarios!, measurementMetrics!, measurementScenarios!, execution);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            var artifact = new
            {
                Status = failure is null ? "passed" : "failed", FailureKind = failure?.GetType().Name,
                WorkloadClass = PerformanceWorkloadContracts.NormalExpectedTraffic,
                ExecutionProfile = new
                {
                    SuiteProfile = suite.Profile.ToString(), Run = run.RunNumber, Mode = run.Mode.ToString(), run.ClientConcurrency,
                    run.ArrivalRatePerSecond, WarmupSeconds = run.Warmup.Duration.TotalSeconds, MeasurementSeconds = run.Measurement.Duration.TotalSeconds,
                    suite.MaximumStartedRequestsPerPhase, suite.MaximumPostRequestsPerRun
                },
                Dataset = new { Profile = suite.DatasetProfile.ToString(), suite.Seed },
                WorkerProfile = ApiBenchmarkWorkerProfiles.GetMetadata(suite.WorkerProfile),
                Environment = new
                {
                    Sdk = Environment.Version.ToString(), Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription, Transport = "Kestrel loopback HTTP", Tls = "not_used"
                },
                HttpMetricsByPhase = new { Warmup = warmupMetrics, Measurement = measurementMetrics },
                ScenarioMetricsByPhase = new { Warmup = warmupScenarios, Measurement = measurementScenarios }, Execution = execution,
                SqlCommandDurationByPhase = new { Warmup = warmupSql, Measurement = measurementSql },
                NpgsqlPoolStateByPhase = new { Warmup = warmupPool, Measurement = measurementPool },
                PostgreSqlSampledLockWaitOccupancyByPhase = new { Warmup = warmupLocks, Measurement = measurementLocks }, RetainedRequestSamples = 0
            };
            string name = $"api-load-{suite.Profile}-{run.RunNumber}-{run.Mode}-{run.ClientConcurrency}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json";
            string path = await ApiLoadSuiteArtifactWriter.WriteAsync(suite.ResultDirectory, name, artifact);
            output.WriteLine($"API load result: {path}");
        }
    }

    private static void EnsureSuccessfulRun(ApiLoadTestHttpMetrics warmupMetrics,
        IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics> warmupScenarios,
        ApiLoadTestHttpMetrics measurementMetrics,
        IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics> measurementScenarios,
        ApiLoadTestExecutionResult execution)
    {
        ApiLoadSuiteAcceptance.EnsureExecutionComplete(execution);
        EnsureSuccessfulPhase(warmupMetrics, warmupScenarios);
        EnsureSuccessfulPhase(measurementMetrics, measurementScenarios);
        measurementMetrics.Successful.ShouldBeGreaterThan(0);
    }

    private static void EnsureSuccessfulPhase(ApiLoadTestHttpMetrics metrics, IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics> scenarios)
    {
        PerformanceWorkloadContracts.EnsureNormalExpectedTrafficHasNoFailures(metrics);
        // Short validation windows do not promise that every low-weight scenario appears;
        // FullBaseline supplies the statistically meaningful coverage. Any observed failure still fails.
        scenarios.Values.ShouldAllBe(scenario => scenario.FailureCount == 0);
    }
}

internal static class ApiLoadSuiteAcceptance
{
    internal static void EnsureExecutionComplete(ApiLoadTestExecutionResult execution)
    {
        execution.WasCancelled.ShouldBeFalse();
        execution.ShutdownGracePeriodElapsed.ShouldBeFalse();
        foreach (ApiLoadTestPhaseExecutionSummary phase in new[] { execution.Warmup, execution.Measurement })
        {
            phase.CancellationRequested.ShouldBeFalse();
            phase.FaultedCount.ShouldBe(0); phase.CancelledCount.ShouldBe(0); phase.DroppedSaturationCount.ShouldBe(0);
            phase.InFlightAtSummaryCount.ShouldBe(0); phase.UnfinishedAfterGraceCount.ShouldBe(0); phase.SafetyCapExceeded.ShouldBeFalse();
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitApiLoadTestFactAttribute : FactAttribute
{
    public ExplicitApiLoadTestFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(ApiLoadSuiteConfiguration.GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Skip = "Explicit API load suite. Set RUN_API_LOAD_SUITE=1; FullBaseline additionally requires EIAMS_ALLOW_LONG_LOAD_TEST=1.";
        }
    }
}
