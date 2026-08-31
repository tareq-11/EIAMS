using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    private const string AdministratorEmail = "integration-admin@example.com";
    private const string TestPassword = "Password123!";
    private static readonly SemaphoreSlim AdministratorLock = new(1, 1);
    private static AccessTokens? administratorTokens;

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        HttpClient = factory.CreateClient();
    }

    protected HttpClient HttpClient { get; }

    protected sealed record AccessTokens(string AccessToken, string RefreshToken);

    protected sealed record ApiEnvelope<T>(bool Success, T Data);

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

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users", request);
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

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login", request);
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
        await AdministratorLock.WaitAsync();
        try
        {
            if (administratorTokens is null)
            {
                HttpClient.DefaultRequestHeaders.Authorization = null;

                HttpResponseMessage bootstrapResponse = await HttpClient.PostAsJsonAsync(
                    "users/register",
                    new
                    {
                        email = AdministratorEmail,
                        firstName = "Integration",
                        lastName = "Administrator",
                        password = TestPassword
                    });

                if (bootstrapResponse.StatusCode is not System.Net.HttpStatusCode.Created and
                    not System.Net.HttpStatusCode.Forbidden)
                {
                    bootstrapResponse.EnsureSuccessStatusCode();
                }

                administratorTokens = await LoginAsync(AdministratorEmail);
            }
        }
        finally
        {
            AdministratorLock.Release();
        }

        Authenticate(administratorTokens.AccessToken);
    }
}
