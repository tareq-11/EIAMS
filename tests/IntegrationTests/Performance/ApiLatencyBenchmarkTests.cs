using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ApiLatencyBenchmarkTests
{
    private const int WarmIterations = 7;
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
        using HttpClient client = factory.CreateClient();
        client.BaseAddress = new Uri("http://localhost/api/v1/");
        var measurements = new List<ApiLatencyMeasurement>();
        var failures = new List<string>();

        (HttpResponseMessage firstLogin, double firstLoginMs) = await LoginAsync(client);
        firstLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
        string accessToken = await ReadAccessTokenAsync(firstLogin);
        firstLogin.Dispose();

        var repeatedLoginDurations = new List<double>();
        for (int iteration = 0; iteration < 3; iteration++)
        {
            (HttpResponseMessage loginResponse, double elapsedMs) = await LoginAsync(client);
            loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            repeatedLoginDurations.Add(elapsedMs);
            loginResponse.Dispose();
        }

        measurements.Add(CreateMeasurement(
            "POST /api/v1/auth/login",
            firstLoginMs,
            repeatedLoginDurations,
            sqlCommands: null));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        SqlCommandCounterInterceptor commandCounter = factory.Services
            .GetRequiredService<SqlCommandCounterInterceptor>();

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

            var warmDurations = new List<double>(WarmIterations);
            var warmSqlCounts = new List<int>(WarmIterations);
            for (int iteration = 0; iteration < WarmIterations; iteration++)
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

        string resultPath = Path.Combine(Path.GetTempPath(), "eiams-api-latency-results.json");
        await File.WriteAllTextAsync(
            resultPath,
            JsonSerializer.Serialize(new { Measurements = measurements, Failures = failures }, JsonOptions));

        foreach (ApiLatencyMeasurement measurement in measurements.OrderByDescending(item => item.WarmP95Ms))
        {
            output.WriteLine(
                $"{measurement.Name}: cold={measurement.ColdMs:F1}ms, " +
                $"warm median={measurement.WarmMedianMs:F1}ms, p95={measurement.WarmP95Ms:F1}ms, " +
                $"avg SQL={measurement.AverageSqlCommands?.ToString("F1", CultureInfo.InvariantCulture) ?? "n/a"}");
        }

        foreach (string failure in failures)
        {
            output.WriteLine($"FAILED: {failure}");
        }

        output.WriteLine($"JSON result: {resultPath}");
    }

    private static async Task<(HttpResponseMessage Response, double ElapsedMs)> LoginAsync(HttpClient client)
    {
        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response = await client.PostAsJsonAsync("auth/login", new
        {
            email = IntegrationTestWebAppFactory.AdministratorEmail,
            password = IntegrationTestWebAppFactory.AdministratorPassword
        });
        await response.Content.LoadIntoBufferAsync();
        stopwatch.Stop();
        return (response, stopwatch.Elapsed.TotalMilliseconds);
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
        double coldMs,
        IReadOnlyCollection<double> warmDurations,
        double? sqlCommands)
    {
        double[] ordered = warmDurations.OrderBy(value => value).ToArray();
        int medianIndex = ordered.Length / 2;
        int p95Index = (int)Math.Ceiling(ordered.Length * 0.95) - 1;
        return new ApiLatencyMeasurement(
            name,
            coldMs,
            ordered[medianIndex],
            ordered[Math.Max(0, p95Index)],
            ordered.Average(),
            sqlCommands);
    }

    private sealed record ApiLatencyMeasurement(
        string Name,
        double ColdMs,
        double WarmMedianMs,
        double WarmP95Ms,
        double WarmAverageMs,
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
