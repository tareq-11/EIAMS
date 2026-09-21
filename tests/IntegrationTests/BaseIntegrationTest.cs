using System.Net.Http.Headers;
using System.Net.Http.Json;
using Domain.Common;
using Domain.Roles;

namespace IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    private const string TestPassword = IntegrationTestWebAppFactory.AdministratorPassword;
    private AccessTokens? administratorTokens;

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        HttpClient = factory.CreateClient();
        HttpClient.BaseAddress = new Uri("http://localhost/api/v1/");
    }

    protected HttpClient HttpClient { get; }

    protected sealed record AccessTokens(string AccessToken, string RefreshToken);

    protected sealed record ApiEnvelope<T>(bool Success, T Data);

    private sealed record ResourceId(Guid Id);

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";
    protected static string UsernameFor(string email) => $"user-{email[..email.IndexOf('@')]}";

    protected async Task<Guid> RegisterUserAsync(string email)
    {
        await AuthenticateAsAdministratorAsync();

        var request = new
        {
            email,
            username = UsernameFor(email),
            firstName = "Test",
            lastName = "User",
            password = TestPassword
        };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("admin/users", request);
        response.EnsureSuccessStatusCode();

        ApiEnvelope<ResourceId>? body =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<ResourceId>>();

        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();

        return body.Data.Id;
    }

    protected async Task<AccessTokens> LoginAsync(string usernameOrEmail)
    {
        var request = new { username = usernameOrEmail, password = TestPassword };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("auth/login", request);
        response.EnsureSuccessStatusCode();

        ApiEnvelope<AccessTokens>? body =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<AccessTokens>>();

        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();

        return body.Data;
    }

    protected async Task<(Guid UserId, AccessTokens Tokens)> RegisterAndLoginAsync()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AssignEnterpriseAdministratorAsync(userId);
        AccessTokens tokens = await LoginAsync(email);

        return (userId, tokens);
    }

    protected async Task AssignEnterpriseAdministratorAsync(Guid userId)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("admin/user-role-scopes", new
        {
            userId,
            roleId = WellKnownRoles.AdministratorId,
            scopeType = "Enterprise",
            scopeId = (Guid?)null
        });
        response.EnsureSuccessStatusCode();
    }

    protected void Authenticate(string accessToken)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    protected async Task AuthenticateAsAdministratorAsync()
    {
        if (administratorTokens is null)
        {
            HttpClient.DefaultRequestHeaders.Authorization = null;
            administratorTokens = await LoginAsync(IntegrationTestWebAppFactory.AdministratorEmail);
        }

        Authenticate(administratorTokens.AccessToken);
    }
}
