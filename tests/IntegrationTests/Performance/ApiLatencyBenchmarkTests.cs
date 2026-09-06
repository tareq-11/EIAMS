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
        using WebApplicationFactory<Program> benchmarkFactory = factory.CreateSiblingFactory(commandCounter);
        benchmarkFactory.UseKestrel(0);
        using HttpClient client = benchmarkFactory.CreateClient();
        client.BaseAddress = new Uri(client.BaseAddress!, "api/v1/");
        int sampleIterations = GetSampleIterations();
        var measurements = new List<ApiLatencyMeasurement>();
        var failures = new List<string>();

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

        var repeatedLoginDurations = new List<double>(sampleIterations);
        for (int iteration = 0; iteration < sampleIterations; iteration++)
        {
            (HttpResponseMessage loginResponse, double elapsedMs) = await LoginAsync(client);
            if (loginResponse.StatusCode != HttpStatusCode.OK)
            {
                failures.Add($"POST auth/login: sample HTTP {(int)loginResponse.StatusCode}");
                loginResponse.Dispose();
                break;
            }

            repeatedLoginDurations.Add(elapsedMs);
            loginResponse.Dispose();
        }

        if (repeatedLoginDurations.Count > 0)
        {
            measurements.Add(CreateMeasurement(
                "POST /api/v1/auth/login [valid]",
                firstLoginMs,
                repeatedLoginDurations,
                sqlCommands: null));
        }

        await MeasureRejectedLoginAsync(
            client,
            "POST /api/v1/auth/login [missing-user]",
            $"missing-{Guid.NewGuid():N}@example.com",
            IntegrationTestWebAppFactory.AdministratorPassword,
            HttpStatusCode.NotFound,
            sampleIterations,
            measurements,
            failures);
        await MeasureRejectedLoginAsync(
            client,
            "POST /api/v1/auth/login [wrong-password]",
            IntegrationTestWebAppFactory.AdministratorEmail,
            "WrongPassword1!",
            HttpStatusCode.NotFound,
            sampleIterations,
            measurements,
            failures);
        (string suspendedEmail, string suspendedPassword) = await CreateSuspendedUserAsync();
        await MeasureRejectedLoginAsync(
            client,
            "POST /api/v1/auth/login [suspended]",
            suspendedEmail,
            suspendedPassword,
            HttpStatusCode.Forbidden,
            sampleIterations,
            measurements,
            failures);

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
            (HttpStatusCode coldStatus, double coldMs, _) = await GetAsync(client, endpoint, commandCounter);
            if (coldStatus != HttpStatusCode.OK)
            {
                failures.Add($"GET {endpoint}: HTTP {(int)coldStatus}");
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

            var warmDurations = new List<double>(sampleIterations);
            var warmSqlCounts = new List<int>(sampleIterations);
            for (int iteration = 0; iteration < sampleIterations; iteration++)
            {
                (HttpStatusCode status, double elapsedMs, int sqlCommands) =
                    await GetAsync(client, endpoint, commandCounter);
                if (status != HttpStatusCode.OK)
                {
                    failures.Add($"GET {endpoint}: warm HTTP {(int)status}");
                    warmDurations.Clear();
                    break;
                }
                warmDurations.Add(elapsedMs);
                warmSqlCounts.Add(sqlCommands);
            }

            if (warmDurations.Count == 0)
            {
                continue;
            }

            measurements.Add(CreateMeasurement(
                $"GET {endpoint}",
                coldMs,
                warmDurations,
                warmSqlCounts.Average()));
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
            WarmupIterations,
            SampleIterations = sampleIterations,
            Measurements = measurements,
            Failures = failures
        };
        await File.WriteAllTextAsync(
            resultPath,
            JsonSerializer.Serialize(benchmarkRun, JsonOptions));

        foreach (ApiLatencyMeasurement measurement in measurements.OrderByDescending(item => item.WarmP95Ms))
        {
            output.WriteLine(
                $"{measurement.Name}: first-observed={measurement.FirstObservedMs:F1}ms, " +
                $"warm p50={measurement.WarmP50Ms:F1}ms, p95={measurement.WarmP95Ms:F1}ms, " +
                $"p99={measurement.WarmP99Ms:F1}ms, " +
                $"avg SQL={measurement.AverageSqlCommands?.ToString("F1", CultureInfo.InvariantCulture) ?? "n/a"}");
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
        List<string> failures)
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

        var durations = new List<double>(sampleIterations);
        for (int iteration = 0; iteration < sampleIterations; iteration++)
        {
            (HttpResponseMessage response, double elapsedMs) = await LoginAsync(client, email, password);
            using (response)
            {
                if (response.StatusCode != expectedStatus)
                {
                    failures.Add($"{name}: sample HTTP {(int)response.StatusCode}");
                    return;
                }
            }

            durations.Add(elapsedMs);
        }

        measurements.Add(CreateMeasurement(name, firstObservedMs, durations, sqlCommands: null));
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
        double firstObservedMs,
        IReadOnlyCollection<double> warmDurations,
        double? sqlCommands)
    {
        double[] ordered = warmDurations.OrderBy(value => value).ToArray();
        return new ApiLatencyMeasurement(
            name,
            firstObservedMs,
            Percentile(ordered, 0.50),
            Percentile(ordered, 0.95),
            Percentile(ordered, 0.99),
            ordered.Average(),
            ordered.Length,
            sqlCommands);
    }

    private static double Percentile(double[] orderedValues, double percentile)
    {
        int index = Math.Max(0, (int)Math.Ceiling(orderedValues.Length * percentile) - 1);
        return orderedValues[index];
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

    private sealed record ApiLatencyMeasurement(
        string Name,
        double FirstObservedMs,
        double WarmP50Ms,
        double WarmP95Ms,
        double WarmP99Ms,
        double WarmAverageMs,
        int SampleCount,
        double? AverageSqlCommands);
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
