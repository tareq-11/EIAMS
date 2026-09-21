using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions.Authentication;
using Domain.Permissions;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using SharedKernel;
using Testcontainers.PostgreSql;

namespace IntegrationTests.Security;

public sealed class BootstrapAndAdministratorSafetyTests
{
    private const string BootstrapEmail = "bootstrap@example.com";
    private const string BootstrapUsername = "bootstrap-admin";
    private const string BootstrapPassword = "Bootstrap123!";
    private static readonly string RecoveryToken = Convert.ToBase64String(new byte[32]);
    private static readonly string[] EnterpriseScope = ["Enterprise"];

    [Fact]
    public async Task Recovery_Should_CreateOnlyOneNewAdministrator_RevokeRefreshTokens_AndAuditTheOperation()
    {
        var factory = new EmptySystemWebAppFactory(bootstrapEnabled: false, recoveryEnabled: true);

        try
        {
            await factory.StartAsync();
            using HttpClient firstClient = factory.CreateApiClient();
            using HttpClient secondClient = factory.CreateApiClient();
            await firstClient.GetAsync("health/live");
            await factory.SeedOrdinaryUserWithRefreshTokenAsync();

#pragma warning disable CA2025 // Both client-bound tasks are awaited together before either client leaves scope.
            Task<RegistrationAttempt> firstRequest = RecoverAsync(firstClient, "first-recovery@example.com");
            Task<RegistrationAttempt> secondRequest = RecoverAsync(secondClient, "second-recovery@example.com");
#pragma warning restore CA2025
            RegistrationAttempt[] responses = await Task.WhenAll(firstRequest, secondRequest);

            responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            responses.Count(response => response.StatusCode == HttpStatusCode.Forbidden).ShouldBe(1);

            string administratorEmail = responses.Single(response => response.StatusCode == HttpStatusCode.Created).Email;
            await factory.AssertRecoveryStateAsync();
            LoginResponse login = await LoginAsync(firstClient, administratorEmail, BootstrapPassword);
            login.Data.AccessToken.ShouldNotBeNullOrWhiteSpace();

            RegistrationAttempt replay = await RecoverAsync(firstClient, "replayed-recovery@example.com");
            replay.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        finally
        {
            await factory.ShutdownAsync();
        }
    }

    [Fact]
    public async Task PublicRegistration_Should_NotExist_AndSwagger_ShouldNotExposeIt()
    {
        var factory = new EmptySystemWebAppFactory(bootstrapEnabled: false);

        try
        {
            await factory.StartAsync();
            using HttpClient client = factory.CreateApiClient();

            using HttpResponseMessage removedEndpoint = await client.PostAsJsonAsync("admin/users/register", new
            {
                email = BootstrapEmail,
                username = BootstrapUsername,
                firstName = "Bootstrap",
                lastName = "Administrator",
                password = BootstrapPassword
            });
            removedEndpoint.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);

            string swagger = await client.GetStringAsync("/swagger/v1/swagger.json");
            swagger.ShouldNotContain("/api/v1/admin/users/register");
            swagger.ShouldNotContain("RegisterController");
        }
        finally
        {
            await factory.ShutdownAsync();
        }
    }

    [Fact]
    public async Task EnabledBootstrap_ShouldRejectWeakConfiguration_BeforeHostStarts()
    {
        var factory = new EmptySystemWebAppFactory(bootstrapEnabled: true, bootstrapPassword: "weak");

        try
        {
            await factory.StartAsync();
            Should.Throw<OptionsValidationException>(() => factory.CreateApiClient());
        }
        finally
        {
            await factory.ShutdownAsync();
        }
    }

    [Fact]
    public async Task EnabledBootstrap_ShouldCreateIdempotentSystemAdministrator_WithExactStructuralGrants()
    {
        var factory = new EmptySystemWebAppFactory(bootstrapEnabled: true);

        try
        {
            await factory.StartAsync();
            using HttpClient client = factory.CreateApiClient();
            (await client.GetAsync("health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);

            LoginResponse? login = await LoginAsync(client);
            login.ShouldNotBeNull();
            login.Data.AccessToken.ShouldNotBeNullOrWhiteSpace();

            IHostedService seeder = factory.Services
                .GetServices<IHostedService>()
                .Single(service => service.GetType().Name == "BootstrapAdministratorSeeder");
            await seeder.StartAsync(CancellationToken.None);

            using (IServiceScope scope = factory.Services.CreateScope())
            {
                ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                User? user = await context.Users.SingleAsync(item => item.Email == BootstrapEmail);
                user.Username.ShouldBe(BootstrapUsername);

                (await context.UserRoleScopes.CountAsync(item => item.UserId == user.Id)).ShouldBe(1);
                UserRoleScope assignment = await context.UserRoleScopes.SingleAsync(item => item.UserId == user.Id);
                assignment.RoleId.ShouldBe(WellKnownRoles.AdministratorId);
                assignment.ScopeType.ShouldBe(Domain.Common.ScopeType.Enterprise);
                assignment.ScopeId.ShouldBeNull();

                Guid[] expectedPermissionIds = WellKnownDottedPermissions
                    .SystemAdministratorPermissionIds
                    .ToArray();
                var assignedPermissions = await context.RolePermissions
                    .Where(permission => permission.RoleId == WellKnownRoles.AdministratorId)
                    .Join(
                        context.Permissions,
                        rolePermission => rolePermission.PermissionId,
                        permission => permission.Id,
                        (_, permission) => new { permission.Id, permission.Code })
                    .ToListAsync();
                Guid[] actualPermissionIds = assignedPermissions
                    .Where(permission => permission.Code.Contains('.'))
                    .Select(permission => permission.Id)
                    .OrderBy(id => id)
                    .ToArray();
                actualPermissionIds.ShouldBe(expectedPermissionIds.OrderBy(id => id).ToArray());

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);
                using HttpResponseMessage sessionResponse = await client.GetAsync("auth/session");
                sessionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
                using var sessionDocument = JsonDocument.Parse(
                    await sessionResponse.Content.ReadAsStringAsync());
                string[] expectedPermissionCodes = await context.Permissions
                    .Where(permission => expectedPermissionIds.Contains(permission.Id))
                    .Select(permission => permission.Code)
                    .OrderBy(code => code)
                    .ToArrayAsync();
                string[] actualPermissionCodes = sessionDocument.RootElement
                    .GetProperty("data")
                    .GetProperty("permissionCodes")
                    .EnumerateArray()
                    .Select(item => item.GetString()!)
                    .OrderBy(code => code)
                    .ToArray();
                actualPermissionCodes.ShouldBe(expectedPermissionCodes);
                actualPermissionCodes.ShouldNotContain(code =>
                    code.StartsWith("inventory.", StringComparison.Ordinal) ||
                    code.StartsWith("audit.", StringComparison.Ordinal) ||
                    code.StartsWith("report.", StringComparison.Ordinal));
            }

            // Reusing the running host is safe and does not create a second assignment.
            using HttpClient secondClient = factory.CreateApiClient();
            (await secondClient.GetAsync("health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            await factory.ShutdownAsync();
        }
    }

    [Fact]
    public async Task EnabledBootstrap_ShouldFailClosed_WhenExistingUsersHaveNoAdministrator()
    {
        var factory = new EmptySystemWebAppFactory(bootstrapEnabled: true);

        try
        {
            await factory.StartAsync(seedOrdinaryUser: true);
            Should.Throw<InvalidOperationException>(() => factory.CreateApiClient());
        }
        finally
        {
            await factory.ShutdownAsync();
        }
    }

    [Fact]
    public async Task BootstrapAdministrator_ShouldResistAssignmentRoleAndPermissionRemoval()
    {
        var factory = new EmptySystemWebAppFactory(bootstrapEnabled: true);

        try
        {
            await factory.StartAsync();
            using HttpClient client = factory.CreateApiClient();
            LoginResponse login = await LoginAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);

            Guid administratorId;
            using (IServiceScope scope = factory.Services.CreateScope())
            {
                ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                administratorId = await context.Users
                    .Where(user => user.Email == BootstrapEmail)
                    .Select(user => user.Id)
                    .SingleAsync();
            }

            (await client.DeleteAsync($"admin/users/{administratorId}/role-scope"))
                .StatusCode.ShouldBe(HttpStatusCode.Conflict);

            AssignmentResponse? assignment = await client
                .GetFromJsonAsync<AssignmentResponse>($"admin/users/{administratorId}/role-scope");
            assignment.ShouldNotBeNull();
            (await client.DeleteAsync($"admin/user-role-scopes/{assignment.Data.Id}"))
                .StatusCode.ShouldBe(HttpStatusCode.Conflict);

            (await client.PutAsJsonAsync(
                $"admin/roles/{WellKnownRoles.AdministratorId}",
                new
                {
                    name = "Changed Administrator",
                    description = "unsafe",
                    allowedScopeTypes = EnterpriseScope
                })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

            (await client.DeleteAsync(
                $"admin/roles/{WellKnownRoles.AdministratorId}/permissions/{WellKnownDottedPermissions.AdminRoleManageId}"))
                .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
        finally
        {
            await factory.ShutdownAsync();
        }
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client)
        => await LoginAsync(client, BootstrapEmail, BootstrapPassword);

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email, string password)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("auth/login", new
        {
            username = email,
            password
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        LoginResponse? body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.ShouldNotBeNull();
        return body;
    }

    private static async Task<RegistrationAttempt> RecoverAsync(HttpClient client, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "admin/recovery/administrator")
        {
            Content = JsonContent.Create(new
            {
                email,
                username = $"recovery-{email[..email.IndexOf('@')]}",
                firstName = "Recovered",
                lastName = "Administrator",
                password = BootstrapPassword
            })
        };
        request.Headers.Add("X-Administrator-Recovery-Token", RecoveryToken);

        using HttpResponseMessage response = await client.SendAsync(request);
        return new RegistrationAttempt(response.StatusCode, email);
    }

    private sealed record LoginResponse(bool Success, Tokens Data);

    private sealed record Tokens(string AccessToken, string RefreshToken);

    private sealed record RegistrationAttempt(HttpStatusCode StatusCode, string Email);

    private sealed record AssignmentResponse(bool Success, Assignment Data);

    private sealed record Assignment(Guid Id);

    private sealed class EmptySystemWebAppFactory(
        bool bootstrapEnabled,
        bool recoveryEnabled = false,
        string bootstrapPassword = BootstrapPassword) : WebApplicationFactory<Program>
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
            builder.UseSetting("AttachmentStorage:MalwareScan:Policy", "Disabled");
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:Authentication:PermitLimit", "1000");
            builder.UseSetting("BootstrapAdministrator:Enabled", bootstrapEnabled.ToString());
            builder.UseSetting("BootstrapAdministrator:Email", BootstrapEmail);
            builder.UseSetting("BootstrapAdministrator:Username", BootstrapUsername);
            builder.UseSetting("BootstrapAdministrator:FirstName", "Bootstrap");
            builder.UseSetting("BootstrapAdministrator:LastName", "Administrator");
            builder.UseSetting("BootstrapAdministrator:Password", bootstrapPassword);
            builder.UseSetting("AdministratorRecovery:Enabled", recoveryEnabled.ToString());
            builder.UseSetting("AdministratorRecovery:Token", RecoveryToken);
            builder.UseSetting(
                "AdministratorRecovery:ExpiresAtUtc",
                DateTime.UtcNow.AddMinutes(10).ToString("O"));
        }

        internal async Task StartAsync(bool seedOrdinaryUser = false)
        {
            await database.StartAsync();
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options;
            await using ApplicationDbContext context = new(options, new NoOpDomainEventsDispatcher());
            await context.Database.MigrateAsync();
            if (seedOrdinaryUser)
            {
                context.Users.Add(User.Create(
                    Guid.NewGuid(),
                    "ordinary@example.com",
                    "ordinary-user",
                    "Ordinary",
                    "User",
                    "not-a-real-hash-for-startup-seed-test"));
                await context.SaveChangesAsync();
            }
        }

        internal HttpClient CreateApiClient()
        {
            HttpClient client = CreateClient();
            client.BaseAddress = new Uri("http://localhost/api/v1/");
            return client;
        }

        internal async Task SeedOrdinaryUserWithRefreshTokenAsync()
        {
            using IServiceScope scope = Services.CreateScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var user = User.Create(
                Guid.NewGuid(),
                "ordinary-before-recovery@example.com",
                "ordinary-before-recovery",
                "Ordinary",
                "User",
                passwordHasher.Hash(BootstrapPassword));
            context.Users.Add(user);
            context.RefreshTokens.Add(RefreshToken.Create(
                Guid.NewGuid(),
                new string('A', 64),
                user.Id,
                DateTime.UtcNow.AddDays(7),
                DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        internal async Task AssertRecoveryStateAsync()
        {
            using IServiceScope scope = Services.CreateScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            int activeAdministrators = await (
                    from assignment in context.UserRoleScopes
                    join user in context.Users on assignment.UserId equals user.Id
                    where assignment.RoleId == WellKnownRoles.AdministratorId &&
                          assignment.ScopeType == Domain.Common.ScopeType.Enterprise &&
                          user.Status == UserStatus.Active
                    select assignment.Id)
                .CountAsync();

            activeAdministrators.ShouldBe(1);
            (await context.RefreshTokens.CountAsync(token => token.RevokedOnUtc == null)).ShouldBe(0);
            (await context.AuditLogs.AnyAsync(log => log.CommandName == "RecoverAdministratorCommand"))
                .ShouldBeTrue();
        }

        internal async Task ShutdownAsync()
        {
            await DisposeAsync();
            await database.DisposeAsync();
        }

        private sealed class NoOpDomainEventsDispatcher : IDomainEventsDispatcher
        {
            public Task DispatchAsync(
                IEnumerable<IDomainEvent> domainEvents,
                CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
