using System.Net;
using System.Net.Http.Headers;

namespace IntegrationTests.Security;

[Collection(nameof(IntegrationTestCollection))]
public sealed class JwtConfigurationTests : BaseIntegrationTest
{
    public JwtConfigurationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task InvalidBearerToken_Should_Return401Unauthorized()
    {
        // Arrange: Provide invalid token
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("organizations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidToken_Should_Authenticate_ButStillRequirePermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("permissions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
