using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Application.Abstractions.Authentication;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ApiLatencyBenchmarkTests
{
    private const int WarmupIterations = 5;
    private const int DefaultSampleIterations = 100;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IntegrationTestWebAppFactory factory;
    private readonly ITestOutputHelper output;

    public ApiLatencyBenchmarkTests(
        IntegrationTestWebAppFactory factory,
        ITestOutputHelper output)
    {
        this.factory = factory;
        this.output = output;
    }

    [ExplicitApiBenchmarkFact]
    [Trait("Category", "Performance")]
    public async Task MeasureRepresentativeApiLatency()
    {
        DateTime startedAtUtc = DateTime.UtcNow;
        SqlCommandCounterInterceptor commandCounter = factory.Services
            .GetRequiredService<SqlCommandCounterInterceptor>();
        using var poolCollector = new NpgsqlPoolStateCollector();
        using WebApplicationFactory<Program> benchmarkFactory = factory.CreateSiblingFactory(commandCounter);
        var hostStartupAndJitStopwatch = Stopwatch.StartNew();
        benchmarkFactory.UseKestrel(0);
        using HttpClient client = benchmarkFactory.CreateClient();
        hostStartupAndJitStopwatch.Stop();
        client.BaseAddress = new Uri(client.BaseAddress!, "api/v1/");
        int sampleIterations = GetSampleIterations();
        var measurements = new List<ApiLatencyMeasurement>();
        var failures = new List<string>();
        (HttpStatusCode firstDatabaseProbeStatus, double firstDatabaseProbeMs, _) =
            await GetAsync(client, "health/ready", commandCounter);
        if (firstDatabaseProbeStatus != HttpStatusCode.OK)
        {
            failures.Add($"GET health/ready: first database health probe HTTP {(int)firstDatabaseProbeStatus}");
        }

        IReadOnlyList<BenchmarkPhaseResult> phases = BenchmarkPhaseMetadata.Create(
            hostStartupAndJitStopwatch.Elapsed.TotalMilliseconds,
            firstDatabaseProbeMs);

        (HttpResponseMessage firstLogin, double firstLoginMs) = await LoginAsync(client);
        firstLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
        string accessToken = await ReadAccessTokenAsync(firstLogin);
        firstLogin.Dispose();

        for (int iteration = 0; iteration < WarmupIterations; iteration++)
        {
            (HttpResponseMessage loginResponse, _) = await LoginAsync(client);
            loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            loginResponse.Dispose();
        }

        ApiBenchmarkScenarioWindow repeatedLogin = await MeasureLoginWindowAsync(
            client,
            IntegrationTestWebAppFactory.AdministratorEmail,
            IntegrationTestWebAppFactory.AdministratorPassword,
            HttpStatusCode.OK,
            sampleIterations,
            commandCounter, poolCollector);
        measurements.Add(CreateMeasurement(
            "POST /api/v1/auth/login [valid]",
            firstLoginMs,
            repeatedLogin));
        AddWindowFailures("POST auth/login", repeatedLogin.Metrics, failures);

        await MeasureRejectedLoginAsync(
            client,
            "POST /api/v1/auth/login [missing-user]",
            $"missing-{Guid.NewGuid():N}@example.com",
            IntegrationTestWebAppFactory.AdministratorPassword,
            HttpStatusCode.NotFound,
            sampleIterations,
            measurements,
            failures,
            commandCounter, poolCollector);
        await MeasureRejectedLoginAsync(
            client,
            "POST /api/v1/auth/login [wrong-password]",
            IntegrationTestWebAppFactory.AdministratorEmail,
            "WrongPassword1!",
            HttpStatusCode.NotFound,
            sampleIterations,
            measurements,
            failures,
            commandCounter, poolCollector);
        (string suspendedEmail, string suspendedPassword) = await CreateSuspendedUserAsync();
        await MeasureRejectedLoginAsync(
            client,
            "POST /api/v1/auth/login [suspended]",
            suspendedEmail,
            suspendedPassword,
            HttpStatusCode.Forbidden,
            sampleIterations,
            measurements,
            failures,
            commandCounter, poolCollector);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        string[] endpoints =
        [
            "health/live",
            "health/ready",
            "auth/session",
            "admin/users?page=1&pageSize=20",
            "organizations?page=1&pageSize=20",
            "sites?page=1&pageSize=20",
            "organizational-units?page=1&pageSize=20",
            "employees?page=1&pageSize=20",
            "admin/roles?page=1&pageSize=20",
            "admin/permissions?page=1&pageSize=20",
            "catalog/units-of-measure?page=1&pageSize=20",
            "catalog/domains?page=1&pageSize=20",
            "catalog/categories?page=1&pageSize=20",
            "catalog/families?page=1&pageSize=20",
            "catalog/materials?page=1&pageSize=20",
            "warehouses?page=1&pageSize=20",
            "warehouse-documents?page=1&pageSize=20",
            "inventory/balances?page=1&pageSize=20",
            "inventory/movements?page=1&pageSize=20",
            "assets?page=1&pageSize=20",
            "custodies?page=1&pageSize=20",
            "adjustments?page=1&pageSize=20",
            "external-parties?page=1&pageSize=20",
            "counterparts?page=1&pageSize=20",
            "reports/dashboard",
            "reports/inventory?page=1&pageSize=20",
            "reports/documents?page=1&pageSize=20",
            "reports/assets?page=1&pageSize=20",
            "reports/count-adjustments?page=1&pageSize=20",
            "audit-logs?page=1&pageSize=20",
            "audit-logs/cursor?pageSize=20"
        ];

        foreach (string endpoint in endpoints)
        {
            (HttpStatusCode firstRequestStatus, double firstRequestMs, _) = await GetAsync(client, endpoint, commandCounter);
            if (firstRequestStatus != HttpStatusCode.OK)
            {
                failures.Add($"GET {endpoint}: first request HTTP {(int)firstRequestStatus}");
                continue;
            }

            for (int iteration = 0; iteration < WarmupIterations; iteration++)
            {
                (HttpStatusCode status, _, _) = await GetAsync(client, endpoint, commandCounter);
                if (status != HttpStatusCode.OK)
                {
                    failures.Add($"GET {endpoint}: warmup HTTP {(int)status}");
                    break;
                }
            }

            if (failures.Any(failure => failure.StartsWith($"GET {endpoint}:", StringComparison.Ordinal)))
            {
                continue;
            }

            ApiBenchmarkScenarioWindow window = await MeasureGetWindowAsync(
                client,
                endpoint,
                commandCounter,
                poolCollector,
                HttpStatusCode.OK,
                sampleIterations);
            measurements.Add(CreateMeasurement($"GET {endpoint}", firstRequestMs, window));
            AddWindowFailures($"GET {endpoint}", window.Metrics, failures);
        }

        string runId = $"{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}";
        string resultDirectory = Environment.GetEnvironmentVariable("EIAMS_BENCHMARK_RESULTS_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(resultDirectory);
        string resultPath = Path.Combine(resultDirectory, $"eiams-api-latency-{runId}.json");
        var benchmarkRun = new
        {
            RunId = runId,
            Commit = GetBuildVersion(),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow,
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Environment.ProcessorCount,
            Transport = "Kestrel over loopback HTTP; includes the network stack and excludes TLS",
            Phases = phases,
            WarmupIterations,
            SampleIterations = sampleIterations,
            Measurements = measurements,
            Failures = failures,
            MeasurementMetadata = new
            {
                ProcessSnapshots = "CPU, resident-set (RSS/working-set), allocations, GC, and ThreadPool snapshots and deltas are process-wide; they are not endpoint attribution.",
                MeasurementModel = "Single-client sequential closed-loop sampling; throughput is not open-loop or concurrent-capacity throughput.",
                ThroughputDefinitions = "attempted=all started samples/window; completed=HTTP responses/window; successful=expected HTTP responses/window.",
                SqlCommandDurations = "EF/Npgsql command execution duration from DbCommandInterceptor; bounded logarithmic histogram p50/p95/p99 is an approximate upper bound (never below the selected bucket's observed durations). The measurement collector is reset after setup/warmup and snapshotted after each sequential scenario window.",
                NpgsqlPoolWait = "not_available: Npgsql 10.0.3 Meter exposes pool state/timeouts but no connection-acquisition wait duration.",
                NpgsqlPoolState = "Npgsql Meter process-wide aggregate across all pools per snapshot: db.client.connection.count state=idle|used, db.client.connection.max, and phase timeouts. Provider tags are discarded; this is neither endpoint/pool attribution nor pool-wait duration.",
                PostgreSqlLockWait = "not_measured",
                RawSql = "not_measured"
            }
        };
        await File.WriteAllTextAsync(
            resultPath,
            JsonSerializer.Serialize(benchmarkRun, JsonOptions));

        foreach (ApiLatencyMeasurement measurement in measurements.OrderByDescending(item => item.Window.P95Ms))
        {
            output.WriteLine(
                $"{measurement.Name}: first-request-for-scenario={measurement.FirstRequestForScenarioMs:F1}ms, " +
                $"samples={measurement.Window.SampleCount}, p50={measurement.Window.P50Ms?.ToString("F1", CultureInfo.InvariantCulture) ?? "n/a"}ms, " +
                $"p95={measurement.Window.P95Ms?.ToString("F1", CultureInfo.InvariantCulture) ?? "n/a"}ms, " +
                $"p99={measurement.Window.P99Ms?.ToString("F1", CultureInfo.InvariantCulture) ?? "n/a"}ms, " +
                $"attempted_rps={measurement.Window.AttemptedThroughputRequestsPerSecond:F2}, " +
                $"completed_rps={measurement.Window.CompletedThroughputRequestsPerSecond:F2}, " +
                $"successful_rps={measurement.Window.SuccessfulThroughputRequestsPerSecond:F2}, " +
                $"completed_response_payload_bytes={measurement.Window.CompletedResponsePayloadBytes}, " +
                $"avg_payload_bytes_per_completed_response={measurement.Window.AverageResponsePayloadBytesPerCompletedResponse?.ToString("F1", CultureInfo.InvariantCulture) ?? "n/a"}, " +
                $"unexpected_http={measurement.Window.UnexpectedHttpErrorCount}, " +
                $"timeouts_or_cancellations={measurement.Window.TimeoutOrCancellationCount}, " +
                $"transport_failures={measurement.Window.TransportFailureCount}, " +
                $"http_429={measurement.Window.RateLimitedCount}, " +
                $"sql_commands={measurement.SqlCommands.Count}, sql_p95={measurement.SqlCommands.ApproximateP95Ms?.ToString("F3", CultureInfo.InvariantCulture) ?? "n/a"}ms, " +
                $"sql_total={measurement.SqlCommands.TotalDurationMs:F3}ms");
        }

        foreach (string failure in failures)
        {
            output.WriteLine($"FAILED: {failure}");
        }

        output.WriteLine($"JSON result: {resultPath}");
        failures.ShouldBeEmpty();
    }

    private static async Task<(HttpResponseMessage Response, double ElapsedMs)> LoginAsync(HttpClient client)
        => await LoginAsync(
            client,
            IntegrationTestWebAppFactory.AdministratorEmail,
            IntegrationTestWebAppFactory.AdministratorPassword);

    private static async Task<(HttpResponseMessage Response, double ElapsedMs)> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response = await client.PostAsJsonAsync("auth/login", new
        {
            email,
            password
        });
        await response.Content.LoadIntoBufferAsync();
        stopwatch.Stop();
        return (response, stopwatch.Elapsed.TotalMilliseconds);
    }

    private static async Task MeasureRejectedLoginAsync(
        HttpClient client,
        string name,
        string email,
        string password,
        HttpStatusCode expectedStatus,
        int sampleIterations,
        List<ApiLatencyMeasurement> measurements,
        List<string> failures,
        SqlCommandCounterInterceptor commandCounter,
        NpgsqlPoolStateCollector poolCollector)
    {
        (HttpResponseMessage firstResponse, double firstObservedMs) = await LoginAsync(client, email, password);
        using (firstResponse)
        {
            if (firstResponse.StatusCode != expectedStatus)
            {
                failures.Add($"{name}: first HTTP {(int)firstResponse.StatusCode}");
                return;
            }
        }

        for (int iteration = 0; iteration < WarmupIterations; iteration++)
        {
            (HttpResponseMessage response, _) = await LoginAsync(client, email, password);
            using (response)
            {
                if (response.StatusCode != expectedStatus)
                {
                    failures.Add($"{name}: warmup HTTP {(int)response.StatusCode}");
                    return;
                }
            }
        }

        ApiBenchmarkScenarioWindow window = await MeasureLoginWindowAsync(
            client,
            email,
            password,
            expectedStatus,
            sampleIterations,
            commandCounter,
            poolCollector);
        measurements.Add(CreateMeasurement(name, firstObservedMs, window));
        AddWindowFailures(name, window.Metrics, failures);
    }

    private async Task<(string Email, string Password)> CreateSuspendedUserAsync()
    {
        const string password = "SuspendedPassword1!";
        string email = $"suspended-{Guid.NewGuid():N}@example.com";
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = User.Create(
            Guid.NewGuid(),
            email,
            "Suspended",
            "Benchmark",
            passwordHasher.Hash(password));
        user.SetStatus(UserStatus.Suspended);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return (email, password);
    }

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = json.RootElement.GetProperty("data");
        return data.TryGetProperty("access_token", out JsonElement snakeCase)
            ? snakeCase.GetString()!
            : data.GetProperty("accessToken").GetString()!;
    }

    private static async Task<(HttpStatusCode Status, double ElapsedMs, int SqlCommands)> GetAsync(
        HttpClient client,
        string endpoint,
        SqlCommandCounterInterceptor commandCounter)
    {
        commandCounter.Reset();
        var stopwatch = Stopwatch.StartNew();
        using HttpResponseMessage response = await client.GetAsync(endpoint);
        await response.Content.LoadIntoBufferAsync();
        stopwatch.Stop();
        return (response.StatusCode, stopwatch.Elapsed.TotalMilliseconds, commandCounter.CommandCount);
    }

    private static ApiLatencyMeasurement CreateMeasurement(
        string name,
        double firstRequestForScenarioMs,
        ApiBenchmarkScenarioWindow window) =>
        new(name, firstRequestForScenarioMs, window.Metrics, window.SqlCommands, window.PoolState);

    private static async Task<ApiBenchmarkScenarioWindow> MeasureLoginWindowAsync(
        HttpClient client,
        string email,
        string password,
        HttpStatusCode expectedStatus,
        int sampleIterations,
        SqlCommandCounterInterceptor commandCounter,
        NpgsqlPoolStateCollector poolCollector) =>
        await MeasureWindowAsync(
            sampleIterations,
            commandCounter,
            poolCollector,
            () => ObserveAsync(
                () => client.PostAsJsonAsync("auth/login", new { email, password }),
                expectedStatus,
                getSqlCommands: null));

    private static async Task<ApiBenchmarkScenarioWindow> MeasureGetWindowAsync(
        HttpClient client,
        string endpoint,
        SqlCommandCounterInterceptor commandCounter,
        NpgsqlPoolStateCollector poolCollector,
        HttpStatusCode expectedStatus,
        int sampleIterations) =>
        await MeasureWindowAsync(
            sampleIterations,
            commandCounter,
            poolCollector,
            async () =>
            {
                return await ObserveAsync(
                    () => client.GetAsync(endpoint),
                    expectedStatus,
                    () => commandCounter.CommandCount);
            });

    private static async Task<ApiBenchmarkScenarioWindow> MeasureWindowAsync(
        int sampleIterations,
        SqlCommandCounterInterceptor? commandCounter,
        NpgsqlPoolStateCollector? poolCollector,
        Func<Task<ApiBenchmarkObservedSample>> measureSample)
    {
        var samples = new List<ApiBenchmarkSample>(sampleIterations);
        // Explicit phase boundary: setup, first request, and warm-up are excluded.
        commandCounter?.Reset();
        poolCollector?.Reset();
        ApiBenchmarkProcessSnapshot before = ApiLatencyBenchmarkMetrics.CaptureProcessSnapshot();
        var stopwatch = Stopwatch.StartNew();
        for (int iteration = 0; iteration < sampleIterations; iteration++)
        {
            ApiBenchmarkObservedSample observed = await measureSample();
            samples.Add(observed.Sample);
        }
        stopwatch.Stop();
        ApiBenchmarkProcessSnapshot after = ApiLatencyBenchmarkMetrics.CaptureProcessSnapshot();
        return new ApiBenchmarkScenarioWindow(
            ApiLatencyBenchmarkMetrics.Calculate(samples, stopwatch.Elapsed, before, after),
            commandCounter?.Snapshot() ?? new SqlCommandDurationSnapshot(0, 0, 0, 0, 0, null, null, null, 0),
            poolCollector?.Snapshot() ?? new NpgsqlPoolStateSnapshot(false, false, false, 0, 0, 0, 0));
    }

    private static async Task<ApiBenchmarkObservedSample> ObserveAsync(
        Func<Task<HttpResponseMessage>> sendAsync,
        HttpStatusCode expectedStatus,
        Func<int?>? getSqlCommands)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using HttpResponseMessage response = await sendAsync();
            byte[] payload = await response.Content.ReadAsByteArrayAsync();
            stopwatch.Stop();
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            double? retryAfterSeconds = ApiLatencyBenchmarkMetrics.ParseRetryAfterSeconds(
                response.Headers.RetryAfter,
                nowUtc);
            return new ApiBenchmarkObservedSample(
                new ApiBenchmarkSample(
                    stopwatch.Elapsed.TotalMilliseconds,
                    payload.Length,
                    ApiLatencyBenchmarkMetrics.Classify((int)response.StatusCode, (int)expectedStatus, false),
                    (int)response.StatusCode,
                    retryAfterSeconds),
                getSqlCommands?.Invoke());
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new ApiBenchmarkObservedSample(
                new ApiBenchmarkSample(
                    stopwatch.Elapsed.TotalMilliseconds,
                    0,
                    ApiBenchmarkResponseClassification.TimeoutOrCancellation,
                    null,
                    null),
                getSqlCommands?.Invoke());
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            return new ApiBenchmarkObservedSample(
                new ApiBenchmarkSample(
                    stopwatch.Elapsed.TotalMilliseconds,
                    0,
                    ApiBenchmarkResponseClassification.TransportFailure,
                    null,
                    null),
                getSqlCommands?.Invoke());
        }
    }

    private static void AddWindowFailures(
        string scenario,
        ApiLatencyWindowMetrics metrics,
        List<string> failures)
    {
        if (metrics.UnexpectedHttpErrorCount > 0)
        {
            failures.Add($"{scenario}: unexpected HTTP errors={metrics.UnexpectedHttpErrorCount}");
        }
        if (metrics.TimeoutOrCancellationCount > 0)
        {
            failures.Add($"{scenario}: timeouts/cancellations={metrics.TimeoutOrCancellationCount}");
        }
        if (metrics.TransportFailureCount > 0)
        {
            failures.Add($"{scenario}: transport failures={metrics.TransportFailureCount}");
        }
        if (metrics.RateLimitedCount > 0)
        {
            failures.Add($"{scenario}: HTTP 429 responses={metrics.RateLimitedCount}; retry-after={metrics.RetryAfter}");
        }
    }

    private static int GetSampleIterations()
    {
        string? configured = Environment.GetEnvironmentVariable("EIAMS_BENCHMARK_SAMPLES");
        return int.TryParse(configured, CultureInfo.InvariantCulture, out int samples)
            ? Math.Clamp(samples, 10, 1_000)
            : DefaultSampleIterations;
    }

    private static string GetBuildVersion() =>
        typeof(ApiLatencyBenchmarkTests).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

    private sealed record ApiBenchmarkObservedSample(ApiBenchmarkSample Sample, int? SqlCommands);

    private sealed record ApiBenchmarkScenarioWindow(
        ApiLatencyWindowMetrics Metrics,
        SqlCommandDurationSnapshot SqlCommands,
        NpgsqlPoolStateSnapshot PoolState);

    private sealed record ApiLatencyMeasurement(
        string Name,
        double FirstRequestForScenarioMs,
        ApiLatencyWindowMetrics Window,
        SqlCommandDurationSnapshot SqlCommands,
        NpgsqlPoolStateSnapshot PoolState);
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitApiBenchmarkFactAttribute : FactAttribute
{
    public ExplicitApiBenchmarkFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_API_BENCHMARKS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Explicit API benchmark. Set RUN_API_BENCHMARKS=1 to run it.";
        }
    }
}
