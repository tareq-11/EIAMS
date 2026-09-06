using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Domain.Permissions;
using Domain.Roles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace IntegrationTests.Security;

public sealed class BootstrapAndAdministratorSafetyTests
{
    private const string Password = "Bootstrap123!";
    private static readonly string BootstrapToken = Convert.ToBase64String(new byte[32]);
    private static readonly string[] EnterpriseScope = ["Enterprise"];

    [Fact]
    public async Task Bootstrap_Should_ReturnForbidden_WhenOperationalGateIsDisabled()
    {
        var factory = new EmptySystemWebAppFactory(bootstrapEnabled: false);

        try
        {
            await factory.StartAsync();
            using HttpClient client = factory.CreateApiClient();

            RegistrationAttempt response = await RegisterAsync(client, "blocked-bootstrap@example.com");

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            response.UserId.ShouldBeNull();
        }
        finally
        {
            await factory.ShutdownAsync();
        }
    }

    [Fact]
    public async Task BootstrapAndBuiltInAdministrator_Should_ResistConcurrentCreationAndLockout()
    {
        var factory = new EmptySystemWebAppFactory();

        try
        {
            await factory.StartAsync();
            using HttpClient firstClient = factory.CreateApiClient();
            using HttpClient secondClient = factory.CreateApiClient();
            await firstClient.GetAsync("health/live");

#pragma warning disable CA2025 // Both client-bound tasks are awaited together before either client leaves scope.
            Task<RegistrationAttempt> firstRequest = RegisterAsync(firstClient, "first-bootstrap@example.com");
            Task<RegistrationAttempt> secondRequest = RegisterAsync(secondClient, "second-bootstrap@example.com");
#pragma warning restore CA2025
            RegistrationAttempt[] registrationResponses = await Task.WhenAll(firstRequest, secondRequest);

            registrationResponses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            registrationResponses.Count(response => response.StatusCode == HttpStatusCode.Forbidden).ShouldBe(1);

            int winnerIndex = Array.FindIndex(
                registrationResponses,
                response => response.StatusCode == HttpStatusCode.Created);
            string administratorEmail = winnerIndex == 0
                ? "first-bootstrap@example.com"
                : "second-bootstrap@example.com";
            Guid? administratorIdValue = registrationResponses[winnerIndex].UserId;
            administratorIdValue.ShouldNotBeNull();
            Guid administratorId = administratorIdValue.Value;

            LoginResponse? login = await LoginAsync(firstClient, administratorEmail);
            firstClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);

            HttpResponseMessage removeAssignment = await firstClient.DeleteAsync(
                $"admin/users/{administratorId}/role-scope");
            removeAssignment.StatusCode.ShouldBe(HttpStatusCode.Conflict);

            AssignmentResponse? assignment = await firstClient
                .GetFromJsonAsync<AssignmentResponse>($"admin/users/{administratorId}/role-scope");
            assignment.ShouldNotBeNull();
            HttpResponseMessage legacyRevoke = await firstClient.DeleteAsync(
                $"admin/user-role-scopes/{assignment.Data.Id}");
            legacyRevoke.StatusCode.ShouldBe(HttpStatusCode.Conflict);

            HttpResponseMessage alterBuiltInRole = await firstClient.PutAsJsonAsync(
                $"admin/roles/{WellKnownRoles.AdministratorId}",
                new
                {
                    name = "Changed Administrator",
                    description = "unsafe",
                    allowedScopeTypes = EnterpriseScope
                });
            alterBuiltInRole.StatusCode.ShouldBe(HttpStatusCode.Conflict);

            HttpResponseMessage removeManagementPermission = await firstClient.DeleteAsync(
                $"admin/roles/{WellKnownRoles.AdministratorId}/permissions/{WellKnownPermissions.RolesManageId}");
            removeManagementPermission.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
        finally
        {
            await factory.ShutdownAsync();
        }
    }

    private static async Task<RegistrationAttempt> RegisterAsync(HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Add("X-Bootstrap-Token", BootstrapToken);
        using HttpResponseMessage response = await client.PostAsJsonAsync("admin/users/register", new
        {
            email,
            firstName = "Bootstrap",
            lastName = "Administrator",
            password = Password
        });

        Guid? userId = null;

        if (response.StatusCode == HttpStatusCode.Created)
        {
            BootstrapResponse? body = await response.Content.ReadFromJsonAsync<BootstrapResponse>();
            body.ShouldNotBeNull();
            userId = body.Data.Id;
        }

        return new RegistrationAttempt(response.StatusCode, userId);
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("auth/login", new { email, password = Password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        LoginResponse? body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.ShouldNotBeNull();
        return body;
    }

    private sealed record BootstrapResponse(bool Success, Identifier Data);

    private sealed record RegistrationAttempt(HttpStatusCode StatusCode, Guid? UserId);

    private sealed record Identifier(Guid Id);

    private sealed record LoginResponse(bool Success, Tokens Data);

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record AssignmentResponse(bool Success, Assignment Data);

    private sealed record Assignment(Guid Id);

    private sealed class EmptySystemWebAppFactory(bool bootstrapEnabled = true) : WebApplicationFactory<Program>
    {
        private readonly string attachmentStoragePath = Path.Combine(
            Path.GetTempPath(),
            $"eiams-security-attachments-{Guid.NewGuid():N}");
        private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("security-review")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.UseSetting("Jwt:Secret", IntegrationTestWebAppFactory.JwtSecret);
            builder.UseSetting("Jwt:Issuer", IntegrationTestWebAppFactory.JwtIssuer);
            builder.UseSetting("Jwt:Audience", IntegrationTestWebAppFactory.JwtAudience);
            builder.UseSetting("Jwt:ExpirationInMinutes", "60");
            builder.UseSetting("AttachmentStorage:Local:RootPath", attachmentStoragePath);
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Authentication:PermitLimit", "1000");
            builder.UseSetting("BootstrapAdministrator:Enabled", bootstrapEnabled.ToString());
            builder.UseSetting("BootstrapAdministrator:Token", BootstrapToken);
        }

        internal Task StartAsync() => database.StartAsync();

        internal HttpClient CreateApiClient()
        {
            HttpClient client = CreateClient();
            client.BaseAddress = new Uri("http://localhost/api/v1/");
            return client;
        }

        internal async Task ShutdownAsync()
        {
            await base.DisposeAsync();
            await database.DisposeAsync();

            if (Directory.Exists(attachmentStoragePath))
            {
                Directory.Delete(attachmentStoragePath, recursive: true);
            }
        }
    }
}
