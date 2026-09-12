using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace IntegrationTests.Performance;

public sealed class ApiSoakSuiteConfigurationTests
{
    [Fact]
    public void QuickValidation_IsExplicitAndBounded()
    {
        var suite = ApiSoakSuiteConfiguration.FromEnvironment(Name => Name == "RUN_API_SOAK_SUITE" ? "1" : null);
        suite.Profile.ShouldBe(ApiSoakSuiteProfile.QuickValidation);
        suite.DatasetProfile.ShouldBe(DatasetProfile.Small);
        suite.Run.Measurement.Duration.ShouldBe(TimeSpan.FromSeconds(12));
        suite.Run.ClientConcurrency.ShouldBe(2);
        suite.Run.ScenarioMix.ShouldNotContain(weight => weight.Scenario == ApiLoadTestScenario.Login);
        suite.Run.ScenarioMix.Sum(weight => weight.Weight).ShouldBe(100);
    }

    [Fact]
    public void FullSoak_RequiresSecondGateAndDurationIsStrictlyBounded()
    {
        Should.Throw<InvalidOperationException>(() => ApiSoakSuiteConfiguration.FromEnvironment(_ => null));
        Should.Throw<InvalidOperationException>(() => ApiSoakSuiteConfiguration.FromEnvironment(Name => Name switch { "RUN_API_SOAK_SUITE" => "1", "EIAMS_API_SOAK_PROFILE" => "FullSoak", _ => null }));
        Should.Throw<ArgumentException>(() => ApiSoakSuiteConfiguration.FromEnvironment(Name => Name switch { "RUN_API_SOAK_SUITE" => "1", "EIAMS_API_SOAK_PROFILE" => "FullSoak", "EIAMS_ALLOW_LONG_SOAK_TEST" => "1", _ => null }));
        Should.Throw<ArgumentException>(() => ApiSoakSuiteConfiguration.FromEnvironment(Name => Name switch { "RUN_API_SOAK_SUITE" => "1", "EIAMS_API_SOAK_PROFILE" => "FullSoak", "EIAMS_ALLOW_LONG_SOAK_TEST" => "1", "EIAMS_API_SOAK_DATASET" => "Small", _ => null }));
        Should.Throw<ArgumentException>(() => ApiSoakSuiteConfiguration.FromEnvironment(Name => FullSoakEnvironment(Name, "59")));
        Should.Throw<ArgumentException>(() => ApiSoakSuiteConfiguration.FromEnvironment(Name => FullSoakEnvironment(Name, "121")));
        var suite = ApiSoakSuiteConfiguration.FromEnvironment(Name => FullSoakEnvironment(Name, "60"));
        suite.Run.Measurement.Duration.ShouldBe(TimeSpan.FromMinutes(60));
        suite.DatasetProfile.ShouldBe(DatasetProfile.Medium);
        suite.Run.Mode.ShouldBe(ApiLoadTestMode.ConstantArrivalRate);
        suite.Run.ArrivalRatePerSecond.ShouldBe(10);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("101")]
    public void FullSoak_RequiresAnExplicitBoundedArrivalRate(string? requestsPerSecond)
    {
        Should.Throw<ArgumentException>(() => ApiSoakSuiteConfiguration.FromEnvironment(Name =>
            Name == "EIAMS_API_SOAK_REQUESTS_PER_SECOND" ? requestsPerSecond : FullSoakEnvironment(Name, "60")));
    }

    [Fact]
    public void FullSoak_CapsCoverMaximumDurationAndRejectInsufficientBudgets()
    {
        var accepted = ApiSoakSuiteConfiguration.FromEnvironment(Name => FullSoakEnvironment(Name, "120"));
        accepted.MaximumStartedRequests.ShouldBeGreaterThanOrEqualTo(792_000);
        accepted.MaximumPostRequests.ShouldBeGreaterThanOrEqualTo(79_200);
        Should.Throw<ArgumentException>(() => ApiSoakSuiteConfiguration.FromEnvironment(Name => Name == "EIAMS_API_SOAK_MAX_STARTED_REQUESTS" ? "1000" : FullSoakEnvironment(Name, "120")));
        Should.Throw<ArgumentException>(() => ApiSoakSuiteConfiguration.FromEnvironment(Name => Name == "EIAMS_API_SOAK_MAX_POST_REQUESTS" ? "1000" : FullSoakEnvironment(Name, "120")));
    }

    [Fact]
    public void UnsafeResultDirectoryAndQuickDuration_AreRejected()
    {
        Should.Throw<ArgumentException>(() => ApiSoakSuiteConfiguration.FromEnvironment(Name => Name switch { "RUN_API_SOAK_SUITE" => "1", "EIAMS_API_SOAK_RESULT_DIR" => "/unsafe", _ => null }));
        Should.Throw<ArgumentException>(() => ApiSoakSuiteConfiguration.FromEnvironment(Name => Name switch { "RUN_API_SOAK_SUITE" => "1", "EIAMS_API_SOAK_DURATION_SECONDS" => "21", _ => null }));
    }

    [Theory]
    [InlineData("{\"email\":\"user@example.test\"}")]
    [InlineData("{\"id\":\"11111111-1111-1111-1111-111111111111\"}")]
    [InlineData("{\"value\":\"https://example.test/path?x=1\"}")]
    [InlineData("{\"password\":\"redacted\"}")]
    [InlineData("{\"value\":\"SELECT * FROM users\"}")]
    [InlineData("{\"request_path\":\"aggregate-only\"}")]
    [InlineData("{\"value\":\"/api/v1/organizations\"}")]
    public void ArtifactSafetyValidator_RejectsSensitiveOrRawValues(string json)
    {
        Should.Throw<ArgumentException>(() => ApiSoakArtifactSafetyValidator.EnsureSafe(json));
    }

    [Fact]
    public void ArtifactSafetyValidator_AllowsSafeAggregateWords()
    {
        Should.NotThrow(() => ApiSoakArtifactSafetyValidator.EnsureSafe("{\"PoolTimeouts\":0,\"TransportFailures\":0,\"Limitations\":\"aggregate-only\"}"));
        Should.NotThrow(() => ApiSoakArtifactSafetyValidator.EnsureSafe("{\"ComparisonBasis\":\"removed from this test host\"}"));
    }

    [Fact]
    public async Task ArtifactWriter_ValidationFailureWritesOnlyMinimalFailedArtifact()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eiams-api-soak-results", Guid.NewGuid().ToString("N"));
        try
        {
            await Should.ThrowAsync<ArgumentException>(() => ApiSoakArtifactWriter.WriteValidatedAsync(directory,
                "unsafe.json", new { Status = "passed", Value = "SELECT * FROM users" }));

            string json = await File.ReadAllTextAsync(Path.Combine(directory, "unsafe.json"));
            ApiSoakArtifactSafetyValidator.EnsureSafe(json);
            using var document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("Status").GetString().ShouldBe("failed");
            document.RootElement.GetProperty("FailureKind").GetString().ShouldBe("ArtifactSafetyValidation");
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
    public async Task AggregateLatency_IncludesSuccessfulScenariosExcludesFailuresAndResets()
    {
        using var handler = new SoakResponseHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/v1/") };
        using var adapter = new ApiLoadTestHttpAdapter(client, "admin@example.test", "not-logged");
        await adapter.ExecuteAsync(ApiLoadTestScenario.Login, CancellationToken.None);
        await adapter.ExecuteAsync(ApiLoadTestScenario.ReadList, CancellationToken.None);
        (await adapter.ExecuteAsync(ApiLoadTestScenario.Report, CancellationToken.None)).Succeeded.ShouldBeFalse();
        adapter.GetAggregateLatency().ApproximateP50Ms.ShouldNotBeNull();
        adapter.GetMetrics().Successful.ShouldBe(2);
        adapter.ResetMetrics();
        adapter.GetAggregateLatency().ApproximateP50Ms.ShouldBeNull();
    }

    [Fact]
    public void ManagedMemorySampler_DisposeStopsBackgroundSamplingWithoutExplicitStop()
    {
        var sampler = new ApiSoakTestSmokeTests.ManagedMemorySampler();
        sampler.Start();
        sampler.Dispose();
        sampler.IsStopped.ShouldBeTrue();
        sampler.IsSamplingTaskCompleted.ShouldBeTrue();
    }

    private static string? FullSoakEnvironment(string name, string minutes) => name switch
    {
        "RUN_API_SOAK_SUITE" => "1",
        "EIAMS_API_SOAK_PROFILE" => "FullSoak",
        "EIAMS_ALLOW_LONG_SOAK_TEST" => "1",
        "EIAMS_API_SOAK_DATASET" => "Medium",
        "EIAMS_API_SOAK_DURATION_MINUTES" => minutes,
        "EIAMS_API_SOAK_REQUESTS_PER_SECOND" => "10",
        _ => null
    };

    private sealed class SoakResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(request.RequestUri!.AbsolutePath.EndsWith("reports/dashboard", StringComparison.Ordinal)
                ? HttpStatusCode.InternalServerError : HttpStatusCode.OK)
            {
                Content = new StringContent(request.Method == HttpMethod.Post ? "{\"data\":{\"access_token\":\"safe\"}}" : "{}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
