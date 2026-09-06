using System.Net;
using System.Net.Http.Json;
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
    public async Task PublicRegistration_Should_ReturnForbidden_WhenSystemIsInitialized()
    {
        // Arrange
        await AuthenticateAsAdministratorAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "admin/users/register",
            new
            {
                email = UniqueEmail(),
                firstName = "Self",
                lastName = "Registered",
                password = "Password123!"
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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
        await RegisterUserAsync(email);

        // Act
        AccessTokens tokens = await LoginAsync(email);

        // Assert
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_Should_ReturnProblem_WhenPasswordIsInvalid()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "auth/login",
            new { email, password = "WrongPassword1!" });

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnNewTokens()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "auth/refresh",
            new { refreshToken = tokens.RefreshToken });

        // Assert
        response.EnsureSuccessStatusCode();
        ApiEnvelope<AccessTokens>? body =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<AccessTokens>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.Data.RefreshToken.ShouldNotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnProblem_WhenTokenIsInvalid()
    {
        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "auth/refresh",
            new { refreshToken = "this-token-does-not-exist" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConcurrentRefresh_Should_AllowOnlyOneRotation_AndInvalidateTheTokenFamilyAsReplay()
    {
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens originalTokens = await LoginAsync(email);
        HttpClient.DefaultRequestHeaders.Authorization = null;

        string tokenHash;
        await using (AsyncServiceScope tokenProviderScope = factory.Services.CreateAsyncScope())
        {
            tokenHash = tokenProviderScope.ServiceProvider
                .GetRequiredService<ITokenProvider>()
                .HashRefreshToken(originalTokens.RefreshToken);
        }

        Task<HttpResponseMessage>[] refreshRequests;
        await using (IntegrationTestWebAppFactory.PostgresAdvisoryLockLease barrier =
                     await factory.HoldApplicationLockAsync($"security:refresh-token:{tokenHash}"))
        {
            Task<HttpResponseMessage> firstRefresh = HttpClient.PostAsJsonAsync(
                "auth/refresh",
                new { refreshToken = originalTokens.RefreshToken });
            await barrier.WaitUntilContendedAsync();
            Task<HttpResponseMessage> replay = HttpClient.PostAsJsonAsync(
                "auth/refresh",
                new { refreshToken = originalTokens.RefreshToken });
            refreshRequests = [firstRefresh, replay];
            await barrier.ReleaseAsync();
        }

        HttpResponseMessage[] responses = await Task.WhenAll(refreshRequests);

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest).ShouldBe(1);

        HttpResponseMessage successfulResponse = responses.Single(response => response.StatusCode == HttpStatusCode.OK);
        ApiEnvelope<AccessTokens>? successfulBody =
            await successfulResponse.Content.ReadFromJsonAsync<ApiEnvelope<AccessTokens>>();
        successfulBody.ShouldNotBeNull();

        HttpResponseMessage familyTokenResponse = await HttpClient.PostAsJsonAsync(
            "auth/refresh",
            new { refreshToken = successfulBody.Data.RefreshToken });
        familyTokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConcurrentRefreshAndLogout_Should_LeaveNoActiveRefreshToken()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);
        HttpClient.DefaultRequestHeaders.Authorization = null;

        string tokenHash;
        await using (AsyncServiceScope tokenProviderScope = factory.Services.CreateAsyncScope())
        {
            tokenHash = tokenProviderScope.ServiceProvider
                .GetRequiredService<ITokenProvider>()
                .HashRefreshToken(tokens.RefreshToken);
        }

        Task<HttpResponseMessage> refreshRequest;
        Task<HttpResponseMessage> logoutRequest;
        await using (IntegrationTestWebAppFactory.PostgresAdvisoryLockLease barrier =
                     await factory.HoldApplicationLockAsync($"security:refresh-token:{tokenHash}"))
        {
            refreshRequest = HttpClient.PostAsJsonAsync(
                "auth/refresh",
                new { refreshToken = tokens.RefreshToken });
            await barrier.WaitUntilContendedAsync();
            logoutRequest = HttpClient.PostAsJsonAsync(
                "auth/logout",
                new { refreshToken = tokens.RefreshToken });
            await barrier.ReleaseAsync();
        }

        HttpResponseMessage[] responses = await Task.WhenAll(refreshRequest, logoutRequest);

        responses.Single(response => response.RequestMessage?.RequestUri?.AbsolutePath.EndsWith(
            "/auth/logout",
            StringComparison.Ordinal) == true).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using AsyncServiceScope reloadScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = reloadScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool hasActiveToken = await context.RefreshTokens
            .AnyAsync(token => token.UserId == userId && token.RevokedOnUtc == null);

        hasActiveToken.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshQueuedBeforeSuspension_Should_NotLeaveAnActiveRefreshToken()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        Task<HttpResponseMessage> refreshRequest;
        Task<HttpResponseMessage> suspendRequest;
        await using (IntegrationTestWebAppFactory.PostgresAdvisoryLockLease barrier =
                     await factory.HoldApplicationLockAsync($"security:user-session:{userId:D}"))
        {
            refreshRequest = HttpClient.PostAsJsonAsync(
                "auth/refresh",
                new { refreshToken = tokens.RefreshToken });
            await barrier.WaitUntilContendedAsync();
            suspendRequest = HttpClient.PutAsJsonAsync(
                $"admin/users/{userId}",
                new
                {
                    email,
                    firstName = "Test",
                    lastName = "User",
                    status = "Suspended"
                });
            await barrier.ReleaseAsync();
        }

        (await refreshRequest).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await suspendRequest).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.RefreshTokens.AnyAsync(token =>
            token.UserId == userId && token.RevokedOnUtc == null)).ShouldBeFalse();
    }

    [Fact]
    public async Task LoginQueuedAfterSuspension_Should_BeRejectedWithoutCreatingASession()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

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
                    firstName = "Test",
                    lastName = "User",
                    status = "Suspended"
                });
            await barrier.WaitUntilContendedAsync();
#pragma warning disable CA2025 // loginRequest is awaited before anonymousClient leaves this method.
            loginRequest = anonymousClient.PostAsJsonAsync(
                "auth/login",
                new { email, password = IntegrationTestWebAppFactory.AdministratorPassword });
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
        await RegisterUserAsync(email);
        AccessTokens firstSession = await LoginAsync(email);
        AccessTokens secondSession = await LoginAsync(email);
        HttpClient.DefaultRequestHeaders.Authorization = null;

        HttpResponseMessage logout = await HttpClient.PostAsJsonAsync(
            "auth/logout",
            new { refreshToken = firstSession.RefreshToken });
        HttpResponseMessage secondRefresh = await HttpClient.PostAsJsonAsync(
            "auth/refresh",
            new { refreshToken = secondSession.RefreshToken });
        HttpResponseMessage firstRefresh = await HttpClient.PostAsJsonAsync(
            "auth/refresh",
            new { refreshToken = firstSession.RefreshToken });

        logout.StatusCode.ShouldBe(HttpStatusCode.OK);
        firstRefresh.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        secondRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
