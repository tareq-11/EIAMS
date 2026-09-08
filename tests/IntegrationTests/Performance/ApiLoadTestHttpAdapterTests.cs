using System.Net;
using System.Net.Http;

namespace IntegrationTests.Performance;

public sealed class ApiLoadTestHttpAdapterTests
{
    [Fact]
    public void Catalog_ShouldUseLogicalLowCardinalityLabelsAndRejectPost()
    {
        ApiLoadTestRequestCatalog.Get(ApiLoadTestScenario.Login).Label.ShouldBe("login");
        ApiLoadTestRequestCatalog.Get(ApiLoadTestScenario.ReadList).Label.ShouldBe("read-list");
        ApiLoadTestRequestCatalog.Get(ApiLoadTestScenario.ReadDetail, new ApiLoadTestFixtureContext(Guid.Empty)).Label.ShouldBe("read-detail");
        ApiLoadTestRequestCatalog.Get(ApiLoadTestScenario.Report).Label.ShouldBe("report");
        Should.Throw<NotSupportedException>(() => ApiLoadTestRequestCatalog.Get(ApiLoadTestScenario.Post));
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
}
