using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests.Security;

[Collection(nameof(IntegrationTestCollection))]
public sealed class RateLimitingTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public RateLimitingTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
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

    [Fact]
    public async Task AuthenticationRateLimit_Should_Return429EnvelopeAndRetryAfter_WhenExceeded()
    {
        await using WebApplicationFactory<Program> limitedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Global:PermitLimit", "100");
            builder.UseSetting("RateLimiting:Authentication:PermitLimit", "2");
            builder.UseSetting("RateLimiting:Authentication:WindowInSeconds", "60");
        });
        using HttpClient client = limitedFactory.CreateClient();
        client.BaseAddress = new Uri("http://localhost/api/v1/");
        var credentials = new
        {
            email = "rate-limit-missing@example.com",
            password = "Password123!"
        };

        await client.PostAsJsonAsync("auth/login", credentials);
        await client.PostAsJsonAsync("auth/login", credentials);
        HttpResponseMessage rejected = await client.PostAsJsonAsync("auth/login", credentials);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        string body = await rejected.Content.ReadAsStringAsync();
        body.ShouldContain("RATE_LIMIT_EXCEEDED");
    }
}
