using System.Net;
using System.Text.Json;

namespace IntegrationTests.Observability;

[Collection(nameof(IntegrationTestCollection))]
public sealed class HealthEndpointsTests(IntegrationTestWebAppFactory factory)
{
    [Theory]
    [InlineData("health")]
    [InlineData("health/live")]
    [InlineData("health/ready")]
    public async Task HealthEndpoint_ShouldBePublicAndHealthy(string path)
    {
        using HttpClient client = factory.CreateClient();
        client.BaseAddress = new Uri("http://localhost/api/v1/");

        HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl?.NoStore.ShouldBeTrue();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.EnumerateObject().Select(item => item.Name).ShouldBe(["status"]);
    }

    [Theory]
    [InlineData("health")]
    [InlineData("health/live")]
    [InlineData("health/ready")]
    public async Task HealthEndpoint_ShouldAllowHeadButRejectOtherMethods(string path)
    {
        using HttpClient client = factory.CreateClient();
        client.BaseAddress = new Uri("http://localhost/api/v1/");

        using var headRequest = new HttpRequestMessage(HttpMethod.Head, path);
        using HttpResponseMessage head = await client.SendAsync(headRequest);
        using HttpResponseMessage post = await client.PostAsync(path, content: null);

        head.StatusCode.ShouldBe(HttpStatusCode.OK);
        post.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("/metrics")]
    [InlineData("/api/v1/metrics")]
    [InlineData("/debug")]
    [InlineData("/diagnostics")]
    public async Task DiagnosticsAndMetricsEndpoints_ShouldNotBePubliclyMapped(string path)
    {
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
