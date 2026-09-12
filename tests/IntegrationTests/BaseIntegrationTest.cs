using System.Net.Http.Headers;
using System.Net.Http.Json;

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

    protected sealed record ApiEnvelope<T>(bool Success, T Data, ApiMeta Meta);
    protected sealed record ApiMeta(string RequestId, DateTime TimestampUtc);

    private sealed record ResourceId(Guid Id);

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    protected async Task<Guid> RegisterUserAsync(string email)
    {
        await AuthenticateAsAdministratorAsync();

        var request = new
        {
            email,
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

    protected async Task<AccessTokens> LoginAsync(string email)
    {
        var request = new { email, password = TestPassword };

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
        AccessTokens tokens = await LoginAsync(email);

        return (userId, tokens);
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
