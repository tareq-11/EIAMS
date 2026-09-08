using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace IntegrationTests.Performance;

public sealed class ApiLoadTestHttpAdapterTests
{
    [Fact]
    public void Catalog_ShouldUseLogicalLowCardinalityLabelsIncludingPost()
    {
        var fixtureId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var fixture = new ApiLoadTestFixtureContext(fixtureId, "test-run", 100);
        ApiLoadTestRequestCatalog.Get(ApiLoadTestScenario.Login).Label.ShouldBe("login");
        ApiLoadTestRequestCatalog.Get(ApiLoadTestScenario.ReadList).Label.ShouldBe("read-list");
        ApiLoadTestRequestCatalog.Get(ApiLoadTestScenario.ReadDetail, fixture).Label.ShouldBe("read-detail");
        ApiLoadTestRequestCatalog.Get(ApiLoadTestScenario.Report).Label.ShouldBe("report");
        ApiLoadTestCatalogEntry post = ApiLoadTestRequestCatalog.Get(ApiLoadTestScenario.Post, fixture);
        post.Label.ShouldBe("post");
        post.Method.ShouldBe(HttpMethod.Post);
        post.RelativePath.ShouldBe("organizations");
        post.ExpectedStatus.ShouldBe(HttpStatusCode.OK);

        string labels = string.Join(',', Enum.GetValues<ApiLoadTestScenario>()
            .Select(scenario => ApiLoadTestRequestCatalog.Get(scenario, fixture).Label));
        labels.ShouldNotContain(fixtureId.ToString("D"));
        labels.ShouldNotContain(fixture.PostRunNamespace);
    }

    [Theory]
    [InlineData(200, ApiBenchmarkResponseClassification.ExpectedResponse)]
    [InlineData(429, ApiBenchmarkResponseClassification.RateLimited)]
    [InlineData(500, ApiBenchmarkResponseClassification.UnexpectedHttpError)]
    public void ClassifyResponse_ShouldKeepHttpOutcomesDistinct(
        int statusCode,
        ApiBenchmarkResponseClassification expected)
    {
        ApiLoadTestHttpAdapter.ClassifyResponse(statusCode, HttpStatusCode.OK, timedOutOrCancelled: false)
            .ShouldBe(expected);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDisposeAndClassifyAnUnexpectedResponse()
    {
        using var handler = new StaticResponseHandler(HttpStatusCode.InternalServerError, "payload");
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost/api/v1/") };
        using var adapter = new ApiLoadTestHttpAdapter(client, "admin@example.test", "not-logged");

        ApiLoadTestExecutionSample sample = await adapter.ExecuteAsync(ApiLoadTestScenario.Login, CancellationToken.None);

        sample.Succeeded.ShouldBeFalse();
        adapter.GetMetrics().UnexpectedHttp.ShouldBe(1);
        adapter.GetMetrics().CompletedResponsePayloadBytes.ShouldBe(7);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAggregateScenarioMetricsWithoutRetainingSamples()
    {
        const string loginBody = "{\"data\":{\"access_token\":\"test-token\"}}";
        using var handler = new StaticResponseHandler(HttpStatusCode.OK, loginBody);
        using var client = new HttpClient(handler, false) { BaseAddress = new Uri("http://localhost/api/v1/") };
        using var adapter = new ApiLoadTestHttpAdapter(client, "admin@example.test", "not-logged");

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => adapter.ExecuteAsync(ApiLoadTestScenario.Login, CancellationToken.None)));

        ApiLoadTestScenarioMetrics metrics = adapter.GetScenarioMetrics()[ApiLoadTestScenario.Login];
        metrics.Count.ShouldBe(100);
        metrics.SuccessCount.ShouldBe(100);
        metrics.FailureCount.ShouldBe(0);
        metrics.PayloadBytes.ShouldBe(100L * loginBody.Length);
        metrics.ApproximateP50Ms.ShouldNotBeNull();
        metrics.ApproximateP95Ms.ShouldNotBeNull();
        metrics.ApproximateP99Ms.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExcludeRateLimitedOnlyScenarioFromNormalLatencyPercentiles()
    {
        const string rateLimitedBody = "{}";
        using var handler = new StaticResponseHandler(HttpStatusCode.TooManyRequests, rateLimitedBody);
        using var client = new HttpClient(handler, false) { BaseAddress = new Uri("http://localhost/api/v1/") };
        using var adapter = new ApiLoadTestHttpAdapter(client, "admin@example.test", "not-logged");

        await adapter.ExecuteAsync(ApiLoadTestScenario.Login, CancellationToken.None);

        ApiLoadTestScenarioMetrics metrics = adapter.GetScenarioMetrics()[ApiLoadTestScenario.Login];
        metrics.Count.ShouldBe(1);
        metrics.SuccessCount.ShouldBe(0);
        metrics.FailureCount.ShouldBe(1);
        metrics.PayloadBytes.ShouldBe(rateLimitedBody.Length);
        metrics.ApproximateP50Ms.ShouldBeNull();
        metrics.ApproximateP95Ms.ShouldBeNull();
        metrics.ApproximateP99Ms.ShouldBeNull();
        adapter.GetMetrics().RateLimited.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectProtectedReadBeforeSetupWithoutSendingARequest()
    {
        using var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/v1/") };
        using var adapter = new ApiLoadTestHttpAdapter(client, "admin@example.test", "password", new ApiLoadTestFixtureContext(Guid.Empty));

        await Should.ThrowAsync<InvalidOperationException>(() => adapter.ExecuteAsync(ApiLoadTestScenario.ReadDetail, CancellationToken.None));
        handler.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRethrowCallerCancellationWithoutRecordingTimeout()
    {
        using var handler = new CountingHandler(_ => throw new OperationCanceledException());
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/v1/") };
        using var adapter = new ApiLoadTestHttpAdapter(client, "admin@example.test", "password");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => adapter.ExecuteAsync(ApiLoadTestScenario.Login, cancellation.Token));
        adapter.GetMetrics().TimeoutOrCancellation.ShouldBe(0);
        adapter.GetScenarioMetrics()[ApiLoadTestScenario.Login].Count.ShouldBe(0);
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldExcludeSetupTrafficFromMetrics()
    {
        using var handler = new CountingHandler(request => request.RequestUri!.AbsolutePath.EndsWith("auth/login", StringComparison.Ordinal)
            ? JsonResponse()
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/v1/") };
        using var adapter = new ApiLoadTestHttpAdapter(client, "admin@example.test", "password");

        await adapter.AuthenticateAsync(CancellationToken.None);
        adapter.GetMetrics().Completed.ShouldBe(0);
        await adapter.ExecuteAsync(ApiLoadTestScenario.ReadList, CancellationToken.None);
        adapter.GetMetrics().Completed.ShouldBe(1);
        adapter.GetScenarioMetrics()[ApiLoadTestScenario.Login].Count.ShouldBe(0);
        adapter.GetScenarioMetrics()[ApiLoadTestScenario.ReadList].Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_Post_ShouldUseUniqueBoundedBusinessWriteBodiesUnderConcurrency()
    {
        ApiLoadTestFixtureContext fixture = new(Guid.Empty, "parallel-run", 100);
        using var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath.EndsWith("auth/login", StringComparison.Ordinal)
            ? JsonResponse()
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"success\":true}") });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/v1/") };
        using var adapter = new ApiLoadTestHttpAdapter(client, "admin@example.test", "password", fixture);

        await adapter.AuthenticateAsync(CancellationToken.None);
        await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => adapter.ExecuteAsync(ApiLoadTestScenario.Post, CancellationToken.None)));

        RecordedRequest[] posts = handler.Requests
            .Where(request => request.Path == "/api/v1/organizations")
            .ToArray();
        posts.Length.ShouldBe(100);
        string[] codes = posts.Select(request =>
        {
            using var json = JsonDocument.Parse(request.Body);
            return json.RootElement.GetProperty("code").GetString()!;
        }).ToArray();
        codes.Distinct(StringComparer.Ordinal).Count().ShouldBe(100);
        codes.ShouldAllBe(code => code.StartsWith("LT-parallel-run-", StringComparison.Ordinal));
        posts.ShouldAllBe(request => !request.Body.Contains(fixture.OrganizationId.ToString("D"), StringComparison.Ordinal));
        adapter.GetScenarioMetrics()[ApiLoadTestScenario.Post].SuccessCount.ShouldBe(100);
    }

    [Fact]
    public async Task ExecuteAsync_Post_ShouldNotSendARequestAfterItsBoundedBudgetIsExhausted()
    {
        var fixture = new ApiLoadTestFixtureContext(Guid.Empty, "limited", 1);
        using var handler = new CountingHandler(request => request.RequestUri!.AbsolutePath.EndsWith("auth/login", StringComparison.Ordinal)
            ? JsonResponse()
            : new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/v1/") };
        using var adapter = new ApiLoadTestHttpAdapter(client, "admin@example.test", "password", fixture);

        await adapter.AuthenticateAsync(CancellationToken.None);
        await adapter.ExecuteAsync(ApiLoadTestScenario.Post, CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(() => adapter.ExecuteAsync(ApiLoadTestScenario.Post, CancellationToken.None));

        handler.Count.ShouldBe(2); // one excluded setup login and one measured Post
    }

    private static HttpResponseMessage JsonResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("{\"data\":{\"access_token\":\"test-token\"}}")
    };

    private sealed class StaticResponseHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(body) });
    }

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        internal int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed record RecordedRequest(string Path, string Body);

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        internal ConcurrentQueue<RecordedRequest> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? string.Empty;
            Requests.Enqueue(new RecordedRequest(request.RequestUri!.AbsolutePath, body));
            return Task.FromResult(responseFactory(request));
        }
    }
}
