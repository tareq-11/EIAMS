using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ApiSoakTestSmokeTests(IntegrationTestWebAppFactory factory, ITestOutputHelper output)
{
    [ExplicitApiSoakTestFact]
    [Trait("Category", "Performance")]
    public async Task RunExplicitConfigurableApiSoakSuiteAsync()
    {
        var suite = ApiSoakSuiteConfiguration.FromEnvironment();
        SyntheticDatasetManifest? manifest = null;
        ApiLoadTestHttpMetrics? metrics = null;
        ApiLoadTestLatencySummary? latency = null;
        IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics>? scenarioMetrics = null;
        ApiLoadTestExecutionResult? execution = null;
        NpgsqlPoolStateSnapshot? pool = null;
        NpgsqlPoolStateSnapshot? recoveryPool = null;
        PostgreSqlLockWaitSnapshot? locks = null;
        MemoryObservation? memory = null;
        var recovery = new RecoveryObservation("not_started", false, false, null);
        Exception? failure = null;
        try
        {
            string databaseName = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString).Database
                ?? throw new InvalidOperationException("Integration test database name is required.");
            await SyntheticDatasetSeeder.SeedAsync(suite.DatasetProfile,
                new SyntheticDatasetSeedOptions(factory.DatabaseConnectionString, databaseName, "Test", suite.Seed));
            manifest = SyntheticDatasetManifestFactory.Create(suite.DatasetProfile, suite.Seed);
            using var poolCollector = new NpgsqlPoolStateCollector();
            await using var lockSampler = new PostgreSqlLockWaitSampler(factory.DatabaseConnectionString);
            SqlCommandCounterInterceptor commands = factory.Services.GetRequiredService<SqlCommandCounterInterceptor>();
            using IntegrationTestWebAppFactory.BenchmarkProfiledWebAppFactory runFactory = factory.CreateSiblingFactory(commands, suite.WorkerProfile);
            runFactory.UseKestrel(0);
            using HttpClient client = runFactory.CreateClient();
            client.BaseAddress = new Uri(client.BaseAddress!, "api/v1/");
            using var adapter = new ApiLoadTestHttpAdapter(client, IntegrationTestWebAppFactory.AdministratorEmail,
                IntegrationTestWebAppFactory.AdministratorPassword,
                new ApiLoadTestFixtureContext(manifest!.GetOrganizationId(0), ApiSoakSuiteConfiguration.CreateRunNamespace(), suite.MaximumPostRequests));
            await adapter.AuthenticateAsync(CancellationToken.None); // setup is excluded from load aggregates
            using var memorySampler = new ManagedMemorySampler();
            memorySampler.Start();
            execution = await new ApiLoadTestExecutor(new StopwatchApiLoadTestClock(), adapter.ExecuteAsync,
                new ApiLoadTestExecutorOptions(TimeSpan.FromSeconds(10), suite.MaximumStartedRequests)).ExecuteAsync(suite.Run,
                async (_, token) => { commands.Reset(); poolCollector.Reset(); adapter.ResetMetrics(); await lockSampler.StartAsync(token); },
                async (_, _) => { locks = await lockSampler.StopAsync(); });
            memorySampler.Stop();
            metrics = adapter.GetMetrics();
            latency = adapter.GetAggregateLatency();
            scenarioMetrics = adapter.GetScenarioMetrics();
            pool = poolCollector.Snapshot();
            memory = memorySampler.Snapshot();
            ApiLoadSuiteAcceptance.EnsureExecutionComplete(execution);
            PerformanceWorkloadContracts.EnsureNormalExpectedTrafficHasNoFailures(metrics);
            metrics.Successful.ShouldBeGreaterThan(0);
            EnsureHealthyPool(pool);
            locks.ShouldNotBeNull();
            locks.IsAvailable.ShouldBeTrue();
            locks.FailureCount.ShouldBe(0);
            foreach (ApiLoadTestScenarioWeight weight in suite.Run.ScenarioMix)
            {
                scenarioMetrics[weight.Scenario].SuccessCount.ShouldBeGreaterThan(0);
            }

            using HttpResponseMessage ready = await client.GetAsync("health/ready");
            ready.StatusCode.ShouldBe(HttpStatusCode.OK);
            recovery = recovery with { Ready = true };
            adapter.ResetMetrics();
            (await adapter.ExecuteAsync(ApiLoadTestScenario.ReadList, CancellationToken.None)).Succeeded.ShouldBeTrue();
            recoveryPool = poolCollector.Snapshot();
            EnsureHealthyPool(recoveryPool);
            recovery = new RecoveryObservation("passed", true, true, null);
        }
        catch (Exception exception) { failure = exception; throw; }
        finally
        {
            var artifact = new
            {
                Status = failure is null && recovery.Status == "passed" ? "passed" : "failed",
                FailureKind = failure?.GetType().Name,
                Limitations = "Aggregate-only loopback evidence; managed-memory observations are not proof of a leak, and sampled lock waits are not per-request duration.",
                ExecutionProfile = new { SuiteProfile = suite.Profile.ToString(), MeasurementSeconds = suite.Run.Measurement.Duration.TotalSeconds, suite.Run.ClientConcurrency, suite.MaximumStartedRequests, suite.MaximumPostRequests },
                Dataset = new { Profile = suite.DatasetProfile.ToString(), suite.Seed },
                WorkerProfile = ApiBenchmarkWorkerProfiles.GetMetadata(suite.WorkerProfile),
                Environment = new { Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription, Transport = "Kestrel loopback HTTP", Tls = "not_used" },
                Http = new { metrics?.Completed, metrics?.Successful, latency?.ApproximateP50Ms, latency?.ApproximateP95Ms, latency?.ApproximateP99Ms, ThroughputRequestsPerSecond = metrics is null ? (double?)null : metrics.Successful / suite.Run.Measurement.Duration.TotalSeconds, metrics?.UnexpectedHttp, metrics?.RateLimited, metrics?.TimeoutOrCancellation, metrics?.TransportFailures },
                ScenarioMetrics = scenarioMetrics,
                ManagedMemory = memory,
                NpgsqlPool = pool,
                RecoveryNpgsqlPool = recoveryPool,
                PostgreSqlSampledLockWaitOccupancy = locks,
                Recovery = failure is null ? recovery : recovery with { Status = "failed", FailureKind = failure.GetType().Name },
                RetainedRequestSamples = 0
            };
            string path = await ApiSoakArtifactWriter.WriteValidatedAsync(suite.ResultDirectory,
                $"api-soak-{suite.Profile}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json", artifact);
            output.WriteLine($"API soak result: {path}");
        }
    }

    private static void EnsureHealthyPool(NpgsqlPoolStateSnapshot snapshot)
    {
        snapshot.ConnectionCountAvailable.ShouldBeTrue();
        snapshot.ConnectionMaxAvailable.ShouldBeTrue();
        snapshot.ConnectionTimeoutsAvailable.ShouldBeTrue();
        snapshot.PoolTimeouts.ShouldBe(0);
        snapshot.UsedConnections.ShouldBeLessThanOrEqualTo(snapshot.MaxConnections);
    }

    internal sealed record MemoryObservation(long StartBytes, long PeakBytes, long EndBytes, long ObservedEndMinusStartBytes, int Gen0Collections, int Gen1Collections, int Gen2Collections);
    private sealed record RecoveryObservation(string Status, bool Ready, bool RepresentativeRead, string? FailureKind);
    internal sealed class ManagedMemorySampler : IDisposable
    {
        private readonly CancellationTokenSource cancellation = new();
        private readonly object gate = new();
        private long start, peak;
        private int gen0, gen1, gen2, stopped;
        private Task? task;
        internal bool IsStopped => Volatile.Read(ref stopped) != 0;
        internal bool IsSamplingTaskCompleted
        {
            get
            {
                lock (gate)
                {
                    return task?.IsCompleted ?? true;
                }
            }
        }
        internal void Start()
        {
            start = GC.GetTotalMemory(false);
            peak = start;
            gen0 = GC.CollectionCount(0);
            gen1 = GC.CollectionCount(1);
            gen2 = GC.CollectionCount(2);
            lock (gate)
            {
                task = SampleAsync();
            }
        }
        internal void Stop()
        {
            if (Interlocked.Exchange(ref stopped, 1) != 0)
            {
                return;
            }

            cancellation.Cancel();
            Task? samplingTask;
            lock (gate)
            {
                samplingTask = task;
            }

            samplingTask?.GetAwaiter().GetResult();
        }
        internal MemoryObservation Snapshot()
        {
            long end = GC.GetTotalMemory(false);
            return new(start, peak, end, end - start, GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2);
        }
        private async Task SampleAsync()
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    long value = GC.GetTotalMemory(false);
                    peak = Math.Max(Interlocked.Read(ref peak), value);
                    await Task.Delay(250, cancellation.Token);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // Cancellation is the normal end of sampling.
            }
        }
        public void Dispose()
        {
            Stop();
            cancellation.Dispose();
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitApiSoakTestFactAttribute : FactAttribute
{
    public ExplicitApiSoakTestFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(ApiSoakSuiteConfiguration.GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Skip = "Explicit API soak suite. Set RUN_API_SOAK_SUITE=1; FullSoak additionally requires EIAMS_ALLOW_LONG_SOAK_TEST=1.";
        }
    }
}
