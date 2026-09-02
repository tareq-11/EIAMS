using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Domain.Users;
using Infrastructure.Database;
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
}
