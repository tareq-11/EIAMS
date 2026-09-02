using System.Net;

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
    }
}
