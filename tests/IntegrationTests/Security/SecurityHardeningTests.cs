using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        HttpClient.DefaultRequestHeaders.Authorization = null;

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("auth/login", new
        {
            username = UsernameFor(email),
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
    public async Task CookieRefresh_Should_RejectUntrustedOriginWithoutConsumingToken()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        HttpClient.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage login = await HttpClient.PostAsJsonAsync("auth/login", new
        {
            username = UsernameFor(email),
            password = IntegrationTestWebAppFactory.AdministratorPassword
        });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var crossSiteRequest = new HttpRequestMessage(HttpMethod.Post, "auth/refresh");
        crossSiteRequest.Headers.Add("Origin", "https://untrusted.example");
        crossSiteRequest.Headers.Add("Sec-Fetch-Site", "cross-site");
        HttpResponseMessage crossSite = await HttpClient.SendAsync(crossSiteRequest);
        crossSite.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var sameOriginRequest = new HttpRequestMessage(HttpMethod.Post, "auth/refresh");
        sameOriginRequest.Headers.Add("Origin", "http://localhost");
        sameOriginRequest.Headers.Add("Sec-Fetch-Site", "same-origin");
        HttpResponseMessage sameOrigin = await HttpClient.SendAsync(sameOriginRequest);
        sameOrigin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SameOriginBrowserMatrix_ShouldAllowRefreshAndRejectCrossOriginCsrf()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);

        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "auth/login")
        {
            Content = JsonContent.Create(new
            {
                username = UsernameFor(email),
                password = IntegrationTestWebAppFactory.AdministratorPassword
            })
        };
        loginRequest.Headers.Add("Origin", "http://localhost");
        loginRequest.Headers.Add("Sec-Fetch-Site", "same-origin");
        using HttpResponseMessage login = await HttpClient.SendAsync(loginRequest);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        string refreshCookie = login.Headers.GetValues("Set-Cookie").Single(value =>
            value.StartsWith("eiams_refresh_token=", StringComparison.Ordinal));
        refreshCookie.ShouldContain("httponly", Case.Insensitive);
        refreshCookie.ShouldContain("samesite=strict", Case.Insensitive);
        string cookiePair = refreshCookie.Split(';', 2)[0];

        using var crossOriginRefresh = new HttpRequestMessage(HttpMethod.Post, "auth/refresh");
        crossOriginRefresh.Headers.Add("Origin", "https://sibling.example.test");
        crossOriginRefresh.Headers.Add("Sec-Fetch-Site", "same-site");
        crossOriginRefresh.Headers.Add("Cookie", cookiePair);
        using HttpResponseMessage csrfAttempt = await HttpClient.SendAsync(crossOriginRefresh);
        csrfAttempt.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        csrfAttempt.Headers.Contains("Set-Cookie").ShouldBeFalse();

        using var sameOriginRefresh = new HttpRequestMessage(HttpMethod.Post, "auth/refresh");
        sameOriginRefresh.Headers.Add("Origin", "http://localhost");
        sameOriginRefresh.Headers.Add("Sec-Fetch-Site", "same-origin");
        sameOriginRefresh.Headers.Add("Cookie", cookiePair);
        using HttpResponseMessage refreshed = await HttpClient.SendAsync(sameOriginRefresh);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        refreshed.Headers.GetValues("Set-Cookie").ShouldContain(value =>
            value.StartsWith("eiams_refresh_token=", StringComparison.Ordinal));

        using var crossOriginPreflight = new HttpRequestMessage(HttpMethod.Options, "health/live");
        crossOriginPreflight.Headers.Add("Origin", "https://sibling.example.test");
        crossOriginPreflight.Headers.Add("Access-Control-Request-Method", "POST");
        using HttpResponseMessage preflight = await HttpClient.SendAsync(crossOriginPreflight);
        preflight.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task CookieOnlyTransport_Should_OmitResponseTokenAndRejectBodyTransport()
    {
        await using WebApplicationFactory<Program> cookieOnlyFactory = factory.WithWebHostBuilder(_ => { });
        using HttpClient cookieClient = CreateVersionedClient(cookieOnlyFactory);

        HttpResponseMessage login = await cookieClient.PostAsJsonAsync("auth/login", new
        {
            username = IntegrationTestWebAppFactory.AdministratorUsername,
            password = IntegrationTestWebAppFactory.AdministratorPassword
        });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        loginJson.RootElement.GetProperty("data").TryGetProperty("refreshToken", out _).ShouldBeFalse();
        login.Headers.GetValues("Set-Cookie").ShouldContain(value =>
            value.StartsWith("eiams_refresh_token=", StringComparison.Ordinal));

        using HttpClient bodyClient = CreateVersionedClient(cookieOnlyFactory);
        HttpResponseMessage bodyRefresh = await bodyClient.PostAsJsonAsync(
            "auth/refresh",
            new { refreshToken = "body-transport-is-disabled" });
        bodyRefresh.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await bodyRefresh.Content.ReadAsStringAsync()).ShouldContain("REFRESH_TOKEN_BODY_DISABLED");
    }

    [Fact]
    public async Task CookieOnlyTransport_Should_RejectChunkedBody_ForRefreshAndLogout()
    {
        await using WebApplicationFactory<Program> cookieOnlyFactory = factory.WithWebHostBuilder(_ => { });
        using HttpClient client = CreateVersionedClient(cookieOnlyFactory);

        using HttpRequestMessage refresh = new(HttpMethod.Post, "auth/refresh")
        {
            Content = new ChunkedJsonContent("{\"refreshToken\":\"body-token\"}")
        };
        using HttpResponseMessage refreshResponse = await client.SendAsync(refresh);
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refreshResponse.Content.ReadAsStringAsync()).ShouldContain("REFRESH_TOKEN_BODY_DISABLED");
        refreshResponse.Headers.Contains("Set-Cookie").ShouldBeFalse();

        using HttpRequestMessage logout = new(HttpMethod.Post, "auth/logout")
        {
            Content = new ChunkedJsonContent("{\"refreshToken\":\"body-token\"}")
        };
        using HttpResponseMessage logoutResponse = await client.SendAsync(logout);
        logoutResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await logoutResponse.Content.ReadAsStringAsync()).ShouldContain("REFRESH_TOKEN_BODY_DISABLED");
        logoutResponse.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Login_Should_RejectUntrustedOriginBeforeCreatingSessionOrCookie()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int tokenCountBefore = await context.RefreshTokens.CountAsync(token => token.UserId == userId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/login")
        {
            Content = JsonContent.Create(new
            {
                username = UsernameFor(email),
                password = IntegrationTestWebAppFactory.AdministratorPassword
            })
        };
        request.Headers.Add("Origin", "https://untrusted.example");
        request.Headers.Add("Sec-Fetch-Site", "cross-site");
        using HttpResponseMessage response = await HttpClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        (await context.RefreshTokens.CountAsync(token => token.UserId == userId)).ShouldBe(tokenCountBefore);
    }

    [Fact]
    public async Task InvalidRefresh_Should_ClearCanonicalAndLegacyCookiePaths()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/refresh");
        request.Headers.Add("Cookie", "eiams_refresh_token=invalid-token; refreshToken=legacy-token");
        using HttpResponseMessage response = await HttpClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string[] deletedCookies = response.Headers.GetValues("Set-Cookie").ToArray();
        deletedCookies.Count(value => value.StartsWith("eiams_refresh_token=", StringComparison.Ordinal)).ShouldBe(2);
        deletedCookies.Count(value => value.StartsWith("refreshToken=", StringComparison.Ordinal)).ShouldBe(2);
        deletedCookies.ShouldContain(value => value.Contains("path=/api/v1/auth", StringComparison.OrdinalIgnoreCase));
        deletedCookies.ShouldContain(value => value.Contains("path=/users", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProductionLogin_Should_MarkRefreshCookieSecure()
    {
        // Run the production-host startup gate against a pristine disposable database. Other
        // integration tests intentionally persist invalid assignment fixtures in the shared DB.
        await using var isolatedFactory = new IntegrationTestWebAppFactory();
        await isolatedFactory.InitializeAsync();
        await using WebApplicationFactory<Program> productionFactory = isolatedFactory.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production"));
        using HttpClient client = productionFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/api/v1/"),
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.PostAsJsonAsync("auth/login", new
        {
            username = IntegrationTestWebAppFactory.AdministratorUsername,
            password = IntegrationTestWebAppFactory.AdministratorPassword
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("Set-Cookie").Single(value =>
            value.StartsWith("eiams_refresh_token=", StringComparison.Ordinal))
            .ShouldContain("secure", Case.Insensitive);
    }

    [Fact]
    public async Task Session_Should_ExposeOnlyTheSingleAuthoritativeAssignmentProjection()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        HttpClient.DefaultRequestHeaders.Authorization = null;

        HttpResponseMessage login = await HttpClient.PostAsJsonAsync("auth/login", new
        {
            username = UsernameFor(email),
            password = IntegrationTestWebAppFactory.AdministratorPassword
        });
        using var loginDocument = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginDocument.RootElement.GetProperty("data").GetProperty("accessToken").GetString());

        HttpResponseMessage session = await HttpClient.GetAsync("auth/session");
        session.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var sessionDocument = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
        JsonElement data = sessionDocument.RootElement.GetProperty("data");
        data.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["user", "role", "activeScope", "permissionCodes"]);
        data.TryGetProperty("activeRoles", out _).ShouldBeFalse();
        data.TryGetProperty("scopeState", out _).ShouldBeFalse();
        data.TryGetProperty("availableScopes", out _).ShouldBeFalse();
        data.GetProperty("permissionCodes").EnumerateArray()
            .Select(code => code.GetString()!)
            .ShouldAllBe(code => code.Contains('.', StringComparison.Ordinal));
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
            $"{{\"username\":\"oversized-login-user\",\"password\":\"{new string('x', (int)AuthRequestLimits.MaximumBodySize)}\"}}";
        using var content = new StringContent(oversizedJson, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await HttpClient.PostAsync("auth/login", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProductionWithoutConfiguredOrigins_Should_RejectCrossOriginPreflight()
    {
        (IReadOnlyList<Guid> AssignmentIds, Guid RoleId) temporarySetup = await EnsureActiveUsersHaveOneAssignmentAsync();
        try
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
        finally
        {
            await RemoveTemporaryAssignmentsAsync(temporarySetup.AssignmentIds, temporarySetup.RoleId);
        }
    }

    [Fact]
    public async Task ProductionHttpsResponse_Should_EnableHsts()
    {
        (IReadOnlyList<Guid> AssignmentIds, Guid RoleId) temporarySetup = await EnsureActiveUsersHaveOneAssignmentAsync();
        try
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
        finally
        {
            await RemoveTemporaryAssignmentsAsync(temporarySetup.AssignmentIds, temporarySetup.RoleId);
        }
    }

    private async Task<(IReadOnlyList<Guid> AssignmentIds, Guid RoleId)> EnsureActiveUsersHaveOneAssignmentAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<Guid> userIds = await context.Users
            .Where(user => user.Status == UserStatus.Active && !context.UserRoleScopes.Any(assignment => assignment.UserId == user.Id))
            .Select(user => user.Id)
            .ToListAsync();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"Production fixture no-permission role {roleId:N}", null));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Enterprise));
        List<Guid> assignmentIds = [];
        foreach (Guid userId in userIds)
        {
            var assignmentId = Guid.NewGuid();
            context.UserRoleScopes.Add(UserRoleScope.Create(
                assignmentId,
                userId,
                roleId,
                ScopeType.Enterprise,
                null));
            assignmentIds.Add(assignmentId);
        }

        await context.SaveChangesAsync();
        return (assignmentIds, roleId);
    }

    private async Task RemoveTemporaryAssignmentsAsync(IReadOnlyList<Guid> assignmentIds, Guid roleId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<UserRoleScope> assignments = await context.UserRoleScopes
            .Where(assignment => assignmentIds.Contains(assignment.Id))
            .ToListAsync();
        context.UserRoleScopes.RemoveRange(assignments);
        List<RoleAllowedScopeType> allowedScopes = await context.RoleAllowedScopeTypes
            .Where(item => item.RoleId == roleId)
            .ToListAsync();
        context.RoleAllowedScopeTypes.RemoveRange(allowedScopes);
        Role? role = await context.Roles.SingleOrDefaultAsync(item => item.Id == roleId);
        if (role is not null)
        {
            context.Roles.Remove(role);
        }
        await context.SaveChangesAsync();
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
            username = IntegrationTestWebAppFactory.AdministratorUsername,
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

    private sealed class ChunkedJsonContent(string json) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(json));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
