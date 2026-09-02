using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Security;

[Collection(nameof(IntegrationTestCollection))]
public sealed class RateLimitingTests : BaseIntegrationTest
{
    public RateLimitingTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_Should_RespondSuccessfully_UnderNormalRate()
    {
        // Act: Single login request
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("auth/login", new
        {
            email = "nonexistent@example.com",
            password = "Password123!"
        });

        // Assert: Under normal rate, should return 400 (InvalidCredentials) rather than 429
        response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }
}
