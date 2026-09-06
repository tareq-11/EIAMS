using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Infrastructure;

namespace IntegrationTests.Phase10;

[Collection(nameof(IntegrationTestCollection))]
public sealed class UserAdministrationIntegrationTests : BaseIntegrationTest
{
    private const string ManagedPassword = "Password123!";
    private readonly IntegrationTestWebAppFactory factory;

    public UserAdministrationIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task GetUsers_Should_ReturnUnauthorized_WhenRequestHasNoToken()
    {
        HttpResponseMessage response = await HttpClient.GetAsync("admin/users");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdministrationWorkflow_Should_CreateListSuspendAndBlockAllAuthenticationPaths()
    {
        // Arrange: an Enterprise administrator creates an account through the administrative route.
        (Guid administratorId, AccessTokens administratorTokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(administratorId);
        Authenticate(administratorTokens.AccessToken);

        string email = $"Managed-{Guid.NewGuid():N}@Example.com";
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("admin/users", new
        {
            email,
            firstName = "Managed",
            lastName = "User",
            password = ManagedPassword
        });

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        ApiResponse<IdentifierResponse>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiResponse<IdentifierResponse>>();
        created.ShouldNotBeNull();
        Guid managedUserId = created.Data!.Id;

        await GrantEnterpriseAdministratorAsync(managedUserId);
        AccessTokens managedTokens = await LoginAsync(User.NormalizeEmail(email));

        // Act: suspend the managed account through the safe update operation.
        Authenticate(administratorTokens.AccessToken);
        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync($"admin/users/{managedUserId}", new
        {
            email,
            firstName = "Updated",
            lastName = "Account",
            status = "Suspended"
        });

        // Assert: update succeeds and every authentication path is closed.
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpClient.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage loginResponse = await HttpClient.PostAsJsonAsync("auth/login", new
        {
            email,
            password = ManagedPassword
        });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        HttpResponseMessage refreshResponse = await HttpClient.PostAsJsonAsync("auth/refresh", new
        {
            refreshToken = managedTokens.RefreshToken
        });
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        Authenticate(managedTokens.AccessToken);
        HttpResponseMessage staleAccessResponse = await HttpClient.GetAsync("admin/users");
        staleAccessResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        Authenticate(administratorTokens.AccessToken);
        HttpResponseMessage listResponse = await HttpClient.GetAsync(
            $"admin/users?search={Uri.EscapeDataString(email)}&status=Suspended&page=1&pageSize=10");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var listBody = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        JsonElement item = listBody.RootElement.GetProperty("data")[0];
        item.GetProperty("id").GetGuid().ShouldBe(managedUserId);
        item.GetProperty("email").GetString().ShouldBe(User.NormalizeEmail(email));
        item.GetProperty("firstName").GetString().ShouldBe("Updated");
        item.GetProperty("status").GetString().ShouldBe("Suspended");
        listBody.RootElement.GetProperty("pagination").GetProperty("total_items").GetInt32().ShouldBe(1);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool hasActiveRefreshToken = await context.RefreshTokens.AnyAsync(token =>
            token.UserId == managedUserId && token.RevokedOnUtc == null);
        hasActiveRefreshToken.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateUser_Should_ReturnConflict_WhenAdministratorSuspendsOwnAccount()
    {
        // Arrange
        (Guid administratorId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(administratorId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"admin/users/{administratorId}", new
        {
            email = $"self-{Guid.NewGuid():N}@example.com",
            firstName = "System",
            lastName = "Administrator",
            status = "Suspended"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ConcurrentRefreshAndSuspension_Should_LeaveAccountSuspendedWithoutActiveRefreshTokens()
    {
        await AuthenticateAsAdministratorAsync();
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);
        await AuthenticateAsAdministratorAsync();

#pragma warning disable CA2025 // Both client-bound tasks are awaited before the shared client is disposed.
        Task<HttpResponseMessage> suspensionRequest = HttpClient.PutAsJsonAsync($"admin/users/{userId}", new
        {
            email,
            firstName = "Concurrent",
            lastName = "Suspension",
            status = "Suspended"
        });
        Task<HttpResponseMessage> refreshRequest = HttpClient.PostAsJsonAsync("auth/refresh", new
        {
            refreshToken = tokens.RefreshToken
        });
#pragma warning restore CA2025

        HttpResponseMessage[] responses = await Task.WhenAll(suspensionRequest, refreshRequest);

        responses.Single(response => response.RequestMessage?.Method == HttpMethod.Put)
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await context.Users.SingleAsync(candidate => candidate.Id == userId);
        bool hasActiveRefreshToken = await context.RefreshTokens.AnyAsync(token =>
            token.UserId == userId && token.RevokedOnUtc == null);

        user.Status.ShouldBe(UserStatus.Suspended);
        hasActiveRefreshToken.ShouldBeFalse();
    }

    private async Task GrantEnterpriseAdministratorAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserRoleScope? assignment = await context.UserRoleScopes.SingleOrDefaultAsync(item => item.UserId == userId);

        if (assignment is null)
        {
            context.UserRoleScopes.Add(UserRoleScope.Create(
                Guid.NewGuid(),
                userId,
                WellKnownRoles.AdministratorId,
                ScopeType.Enterprise,
                null));
        }
        else
        {
            assignment.ReplaceAssignment(WellKnownRoles.AdministratorId, ScopeType.Enterprise, null);
        }

        await context.SaveChangesAsync();
    }

    private sealed record IdentifierResponse(Guid Id);
}
