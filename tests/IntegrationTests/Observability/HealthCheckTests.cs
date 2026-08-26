using System.Net;

namespace IntegrationTests.Observability;

[Collection(nameof(IntegrationTestCollection))]
public sealed class HealthCheckTests : BaseIntegrationTest
{
    public HealthCheckTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task HealthCheck_Should_Return200Healthy()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
