using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    [Fact]
    public async Task GlobalRateLimit_Should_RunBeforeAuthorizationWork()
    {
        await using WebApplicationFactory<Program> limitedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1");
            builder.UseSetting("RateLimiting:Global:WindowInSeconds", "60");
            builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100");
        });
        using HttpClient client = limitedFactory.CreateClient();
        client.BaseAddress = new Uri("http://localhost/api/v1/");

        HttpResponseMessage first = await client.GetAsync("admin/users");
        HttpResponseMessage second = await client.GetAsync("admin/users");

        first.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        string body = await second.Content.ReadAsStringAsync();
        body.ShouldContain("RATE_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task AuthenticationConcurrencyLimit_Should_RejectOverlappingPasswordVerification()
    {
        using var blockingHasher = new BlockingPasswordHasher();
        await using WebApplicationFactory<Program> limitedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Global:PermitLimit", "100");
            builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100");
            builder.UseSetting("RateLimiting:Authentication:ConcurrencyLimit", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher>(blockingHasher);
            });
        });
        HttpClient client = limitedFactory.CreateClient();
        client.BaseAddress = new Uri("http://localhost/api/v1/");
        var credentials = new
        {
            email = "concurrency-limit-missing@example.com",
            password = "Password123!"
        };

        Task<HttpStatusCode> firstRequest = SendLoginAsync(client, credentials);
        HttpStatusCode? completedStatus = null;

        try
        {
            await blockingHasher.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            using HttpResponseMessage rejected = await client.PostAsJsonAsync("auth/login", credentials);
            rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            (await rejected.Content.ReadAsStringAsync()).ShouldContain("RATE_LIMIT_EXCEEDED");
        }
        finally
        {
            blockingHasher.Release();
            completedStatus = await firstRequest;
            client.Dispose();
        }

        completedStatus.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<HttpStatusCode> SendLoginAsync(HttpClient client, object credentials)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("auth/login", credentials);
        return response.StatusCode;
    }

    private sealed class BlockingPasswordHasher : IPasswordHasher, IDisposable
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim release = new(initialState: false);

        internal Task Entered => entered.Task;

        public string Hash(string password) => throw new NotSupportedException();

        public bool Verify(string password, string passwordHash)
        {
            entered.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(10));
            return false;
        }

        internal void Release() => release.Set();

        public void Dispose() => release.Dispose();
    }
}
