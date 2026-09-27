using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions.Authentication;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

public sealed class UsersTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public UsersTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AdministratorCreateUser_Should_ReturnUserId()
    {
        // Act
        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Assert
        userId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task RegisterAndLogin_Should_Succeed_WhenOrdinaryUserExistsBeforeFirstAdministratorAuthentication()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        context.Users.Add(User.Create(
            Guid.NewGuid(),
            UniqueEmail(),
            "Ordinary",
            "User",
            passwordHasher.Hash(IntegrationTestWebAppFactory.AdministratorPassword)));
        await context.SaveChangesAsync();

        // Act
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();

        // Assert
        userId.ShouldNotBe(Guid.Empty);
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_Should_ReturnAccessAndRefreshTokens()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);

        // Act
        AccessTokens tokens = await LoginAsync(UsernameFor(email));

        // Assert
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_ShouldUseCanonicalUsernameOnly_AndKeepFailureGeneric()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        string username = UsernameFor(email);

        HttpResponseMessage canonicalLogin = await HttpClient.PostAsJsonAsync(
            "auth/login",
            new { username = $"  {username.ToUpperInvariant()}  ", password = IntegrationTestWebAppFactory.AdministratorPassword });
        canonicalLogin.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage emailLogin = await HttpClient.PostAsJsonAsync(
            "auth/login",
            new { username = email, password = IntegrationTestWebAppFactory.AdministratorPassword });
        HttpResponseMessage unknownLogin = await HttpClient.PostAsJsonAsync(
            "auth/login",
            new { username = "unknown-login-user", password = IntegrationTestWebAppFactory.AdministratorPassword });

        emailLogin.StatusCode.ShouldBe(unknownLogin.StatusCode);
        using var emailBody = JsonDocument.Parse(await emailLogin.Content.ReadAsStringAsync());
        using var unknownBody = JsonDocument.Parse(await unknownLogin.Content.ReadAsStringAsync());
        JsonElement emailError = emailBody.RootElement.GetProperty("error");
        JsonElement unknownError = unknownBody.RootElement.GetProperty("error");
        emailBody.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        unknownBody.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        emailError.GetProperty("code").GetString().ShouldBe(unknownError.GetProperty("code").GetString());
        emailError.GetProperty("message").GetString().ShouldBe(unknownError.GetProperty("message").GetString());
        emailError.GetProperty("details").ToString().ShouldBe(unknownError.GetProperty("details").ToString());
        emailError.GetProperty("request_id").GetString().ShouldNotBeNullOrWhiteSpace();
        unknownError.GetProperty("request_id").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_Should_ReturnProblem_WhenPasswordIsInvalid()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "auth/login",
            new { username = UsernameFor(email), password = "WrongPassword1!" });

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnNewTokens()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        await LoginAsync(UsernameFor(email));

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync("auth/refresh", null);

        // Assert
        response.EnsureSuccessStatusCode();
        ApiEnvelope<AccessTokens>? body =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<AccessTokens>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.AccessToken.ShouldNotBeNullOrWhiteSpace();
        response.Headers.GetValues("Set-Cookie").ShouldContain(value => value.StartsWith("eiams_refresh_token=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnProblem_WhenTokenIsInvalid()
    {
        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/refresh");
        request.Headers.Add("Cookie", "eiams_refresh_token=this-token-does-not-exist");
        HttpResponseMessage response = await HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConcurrentRefresh_Should_AllowOnlyOneRotation_AndInvalidateTheTokenFamilyAsReplay()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        AccessTokens originalTokens = await LoginAsync(UsernameFor(email));
        HttpClient.DefaultRequestHeaders.Authorization = null;
        HttpClient firstClient = CreateCookieOnlyClient();
        HttpClient replayClient = CreateCookieOnlyClient();
        string tokenHash = factory.Services.GetRequiredService<ITokenProvider>().HashRefreshToken(originalTokens.RefreshToken);
        HttpResponseMessage firstResponse;
        HttpResponseMessage replayResponse;
        await using (IntegrationTestWebAppFactory.PostgresAdvisoryLockLease barrier =
                         await factory.HoldApplicationLockAsync($"security:refresh-token:{tokenHash}"))
        {
            Task<HttpResponseMessage> firstTask = SendRefreshWithCookieAsync(firstClient, originalTokens.RefreshToken);
            await barrier.WaitUntilContendedAsync();
            Task<HttpResponseMessage> replayTask = SendRefreshWithCookieAsync(replayClient, originalTokens.RefreshToken);
            await barrier.ReleaseAsync();
            firstResponse = await firstTask;
            replayResponse = await replayTask;
        }
        HttpResponseMessage[] responses = [firstResponse, replayResponse];

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest).ShouldBe(1);

        HttpResponseMessage successfulResponse = responses.Single(response => response.StatusCode == HttpStatusCode.OK);
        ApiEnvelope<AccessTokens>? successfulBody =
            await successfulResponse.Content.ReadFromJsonAsync<ApiEnvelope<AccessTokens>>();
        successfulBody.ShouldNotBeNull();

        string rotatedToken = Uri.UnescapeDataString(successfulResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("eiams_refresh_token=", StringComparison.Ordinal))
            .Split(';', 2)[0]["eiams_refresh_token=".Length..]);
        using HttpRequestMessage familyTokenRequest = RefreshRequestWithCookie(rotatedToken);
        HttpResponseMessage familyTokenResponse = await firstClient.SendAsync(familyTokenRequest);
        familyTokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        firstClient.Dispose();
        replayClient.Dispose();
    }

    [Fact]
    public async Task ConcurrentRefreshAndLogout_Should_LeaveNoActiveRefreshToken()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        AccessTokens tokens = await LoginAsync(UsernameFor(email));
        HttpClient.DefaultRequestHeaders.Authorization = null;
        HttpClient refreshClient = CreateCookieOnlyClient();
        HttpClient logoutClient = CreateCookieOnlyClient();
        string tokenHash = factory.Services.GetRequiredService<ITokenProvider>().HashRefreshToken(tokens.RefreshToken);
        HttpResponseMessage refreshResponse;
        HttpResponseMessage logoutResponse;
        await using (IntegrationTestWebAppFactory.PostgresAdvisoryLockLease barrier =
                         await factory.HoldApplicationLockAsync($"security:refresh-token:{tokenHash}"))
        {
            Task<HttpResponseMessage> refreshTask = SendRefreshWithCookieAsync(refreshClient, tokens.RefreshToken);
            await barrier.WaitUntilContendedAsync();
            Task<HttpResponseMessage> logoutTask = SendLogoutWithCookieAsync(logoutClient, tokens.RefreshToken);
            await barrier.ReleaseAsync();
            refreshResponse = await refreshTask;
            logoutResponse = await logoutTask;
        }
        HttpResponseMessage[] responses = [refreshResponse, logoutResponse];

        responses.Single(response => response.RequestMessage?.RequestUri?.AbsolutePath.EndsWith(
            "/auth/logout",
            StringComparison.Ordinal) == true).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using AsyncServiceScope reloadScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = reloadScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool hasActiveToken = await context.RefreshTokens
            .AnyAsync(token => token.UserId == userId && token.RevokedOnUtc == null);

        hasActiveToken.ShouldBeFalse();
        refreshClient.Dispose();
        logoutClient.Dispose();
    }

    [Fact]
    public async Task RefreshQueuedBeforeSuspension_Should_NotLeaveAnActiveRefreshToken()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        AccessTokens tokens = await LoginAsync(UsernameFor(email));
        HttpClient tokenClient = CreateCookieOnlyClient();
        HttpResponseMessage refreshResponse;
        Task<HttpResponseMessage> suspendTask;
        await using (IntegrationTestWebAppFactory.PostgresAdvisoryLockLease barrier =
                         await factory.HoldApplicationLockAsync($"security:user-session:{userId:D}"))
        {
            Task<HttpResponseMessage> refreshTask = SendRefreshWithCookieAsync(tokenClient, tokens.RefreshToken);
            await barrier.WaitUntilContendedAsync();
            suspendTask = HttpClient.PutAsJsonAsync(
                $"admin/users/{userId}",
                new
                {
                    email,
                    username = UsernameFor(email),
                    firstName = "Test",
                    lastName = "User",
                    status = "Suspended"
                });
            await barrier.ReleaseAsync();
            refreshResponse = await refreshTask;
        }
        HttpResponseMessage suspendResponse = await suspendTask;

        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        suspendResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.RefreshTokens.AnyAsync(token =>
            token.UserId == userId && token.RevokedOnUtc == null)).ShouldBeFalse();
        tokenClient.Dispose();
    }

    [Fact]
    public async Task LoginQueuedAfterSuspension_Should_BeRejectedWithoutCreatingASession()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);

        using HttpClient anonymousClient = factory.CreateClient();
        anonymousClient.BaseAddress = new Uri("http://localhost/api/v1/");

        Task<HttpResponseMessage> suspendRequest;
        Task<HttpResponseMessage> loginRequest;
        await using (IntegrationTestWebAppFactory.PostgresAdvisoryLockLease barrier =
                     await factory.HoldApplicationLockAsync($"security:user-session:{userId:D}"))
        {
            suspendRequest = HttpClient.PutAsJsonAsync(
                $"admin/users/{userId}",
                new
                {
                    email,
                    username = UsernameFor(email),
                    firstName = "Test",
                    lastName = "User",
                    status = "Suspended"
                });
            await barrier.WaitUntilContendedAsync();
#pragma warning disable CA2025 // loginRequest is awaited before anonymousClient leaves this method.
            loginRequest = anonymousClient.PostAsJsonAsync(
                "auth/login",
                new { username = UsernameFor(email), password = IntegrationTestWebAppFactory.AdministratorPassword });
#pragma warning restore CA2025
            await barrier.ReleaseAsync();
        }

        (await suspendRequest).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await loginRequest).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.RefreshTokens.AnyAsync(token =>
            token.UserId == userId && token.RevokedOnUtc == null)).ShouldBeFalse();
    }

    [Fact]
    public async Task Logout_Should_RevokeCurrentSessionButKeepOtherDeviceSessionActive()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        AccessTokens firstTokens = await LoginAsync(UsernameFor(email));
        HttpClient.DefaultRequestHeaders.Authorization = null;
        AccessTokens secondTokens = await LoginAsync(UsernameFor(email));
        using HttpClient firstClient = CreateCookieOnlyClient();
        using HttpClient secondClient = CreateCookieOnlyClient();

        using HttpRequestMessage logoutRequest = LogoutRequestWithCookie(firstTokens.RefreshToken);
        using HttpRequestMessage otherDeviceRefreshRequest = RefreshRequestWithCookie(secondTokens.RefreshToken);
        HttpResponseMessage logout = await firstClient.SendAsync(logoutRequest);
        HttpResponseMessage otherDeviceRefresh = await secondClient.SendAsync(otherDeviceRefreshRequest);
        using HttpRequestMessage refreshAfterLogoutRequest = RefreshRequestWithCookie(firstTokens.RefreshToken);
        HttpResponseMessage refreshAfterLogout = await firstClient.SendAsync(refreshAfterLogoutRequest);

        logout.StatusCode.ShouldBe(HttpStatusCode.OK);
        refreshAfterLogout.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        otherDeviceRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private HttpClient CreateCookieOnlyClient()
    {
        HttpClient client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        client.BaseAddress = new Uri("http://localhost/api/v1/");
        return client;
    }

    private static async Task<HttpResponseMessage> SendRefreshWithCookieAsync(HttpClient client, string refreshToken)
    {
        using HttpRequestMessage request = RefreshRequestWithCookie(refreshToken);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendLogoutWithCookieAsync(HttpClient client, string refreshToken)
    {
        using HttpRequestMessage request = LogoutRequestWithCookie(refreshToken);
        return await client.SendAsync(request);
    }
}
