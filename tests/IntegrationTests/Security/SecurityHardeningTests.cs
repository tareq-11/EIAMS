using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using Web.Api.Infrastructure;

namespace IntegrationTests.Security;

[Collection(nameof(IntegrationTestCollection))]
public sealed class SecurityHardeningTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public SecurityHardeningTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Login_Should_IssueRefreshCookieOnlyForAuthPath_WithDefensiveAttributes()
    {
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        HttpClient.DefaultRequestHeaders.Authorization = null;

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("auth/login", new
        {
            email,
            password = IntegrationTestWebAppFactory.AdministratorPassword
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string setCookie = response.Headers.GetValues("Set-Cookie").Single(value =>
            value.StartsWith("eiams_refresh_token=", StringComparison.Ordinal));
        setCookie.ShouldContain("path=/api/v1/auth", Case.Insensitive);
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
        response.Headers.CacheControl?.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task HealthResponse_Should_NotExposeDependencyNamesOrDetailedEntries()
    {
        HttpResponseMessage response = await HttpClient.GetAsync("health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name).ToArray().ShouldBe(["status"]);
        json.ShouldNotContain("postgresql", Case.Insensitive);
        json.ShouldNotContain("entries", Case.Insensitive);
    }

    [Fact]
    public async Task ApiResponse_Should_IncludeDefensiveSecurityHeaders()
    {
        HttpResponseMessage response = await HttpClient.GetAsync("health/live");

        response.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
        response.Headers.GetValues("X-Frame-Options").Single().ShouldBe("DENY");
        response.Headers.GetValues("Referrer-Policy").Single().ShouldBe("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'none'");
    }

    [Fact]
    public async Task Login_Should_RejectOversizedCredentialsBeforePasswordVerification()
    {
        string oversizedJson =
            $"{{\"email\":\"user@example.com\",\"password\":\"{new string('x', (int)AuthRequestLimits.MaximumBodySize)}\"}}";
        using var content = new StringContent(oversizedJson, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await HttpClient.PostAsync("auth/login", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProductionWithoutConfiguredOrigins_Should_RejectCrossOriginPreflight()
    {
        await using WebApplicationFactory<Program> productionFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("AllowedHosts", "api.example.test");
        });
        using HttpClient client = productionFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.example.test"),
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/health/live");
        request.Headers.Add("Origin", "https://untrusted.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task ProductionHttpsResponse_Should_EnableHsts()
    {
        await using WebApplicationFactory<Program> productionFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("AllowedHosts", "api.example.test");
        });
        using HttpClient client = productionFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.example.test"),
            AllowAutoRedirect = false
        });

        HttpResponseMessage response = await client.GetAsync("/api/v1/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Strict-Transport-Security").ShouldBeTrue();
    }

    [Fact]
    public async Task JwtKeyRing_Should_AcceptPreviousKeyDuringOverlap_AndRejectItAfterRemoval()
    {
        const string previousKey = "previous-signing-key-with-at-least-32-bytes";
        const string currentKey = "current-signing-key-with-at-least-32-bytes";

        await using WebApplicationFactory<Program> previousKeyFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Jwt:Secret", previousKey));
        using HttpClient previousKeyClient = CreateVersionedClient(previousKeyFactory);
        HttpResponseMessage loginResponse = await previousKeyClient.PostAsJsonAsync("auth/login", new
        {
            email = IntegrationTestWebAppFactory.AdministratorEmail,
            password = IntegrationTestWebAppFactory.AdministratorPassword
        });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<AccessTokens>? login =
            await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<AccessTokens>>();
        login.ShouldNotBeNull();

        await using WebApplicationFactory<Program> overlapFactory = factory.WithWebHostBuilder(builder =>
            ConfigureJwtKeyRing(builder, "current", legacySecret: previousKey, currentKey));
        using HttpClient overlapClient = CreateVersionedClient(overlapFactory);
        overlapClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);

        HttpResponseMessage acceptedDuringOverlap = await overlapClient.GetAsync("auth/session");
        acceptedDuringOverlap.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using WebApplicationFactory<Program> currentOnlyFactory = factory.WithWebHostBuilder(builder =>
            ConfigureJwtKeyRing(builder, "current", legacySecret: null, currentKey));
        using HttpClient currentOnlyClient = CreateVersionedClient(currentOnlyFactory);
        currentOnlyClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);

        HttpResponseMessage rejectedAfterRemoval = await currentOnlyClient.GetAsync("auth/session");
        rejectedAfterRemoval.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static void ConfigureJwtKeyRing(
        IWebHostBuilder builder,
        string activeKeyId,
        string? legacySecret,
        string? currentKey)
    {
        builder.UseSetting("Jwt:Secret", legacySecret ?? string.Empty);
        builder.UseSetting("Jwt:ActiveKeyId", activeKeyId);
        if (currentKey is not null)
        {
            builder.UseSetting("Jwt:Keys:current", currentKey);
        }
    }

    private static HttpClient CreateVersionedClient(WebApplicationFactory<Program> factory)
    {
        HttpClient client = factory.CreateClient();
        client.BaseAddress = new Uri("http://localhost/api/v1/");
        return client;
    }
}
