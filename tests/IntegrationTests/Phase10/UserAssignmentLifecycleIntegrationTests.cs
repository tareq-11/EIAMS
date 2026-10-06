using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.Organizations;
using Domain.Roles;
using Domain.Sites;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Phase10;

[Collection(nameof(IntegrationTestCollection))]
public sealed class UserAssignmentLifecycleIntegrationTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public UserAssignmentLifecycleIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory) =>
        this.factory = factory;

    [Fact]
    public async Task CreateUser_ShouldReturnAuthoritativeAssignment_AndGetShouldMatch()
    {
        await AuthenticateAsAdministratorAsync();
        string email = UniqueEmail();
        string username = UsernameFor(email);

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync("admin/users", new
        {
            email,
            username,
            firstName = "Lifecycle",
            lastName = "User",
            password = "Password123!",
            roleId = WellKnownRoles.AdministratorId,
            scopeType = "Enterprise",
            scopeId = (Guid?)null
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        ApiEnvelope<CreateUserData>? body = await created.Content.ReadFromJsonAsync<ApiEnvelope<CreateUserData>>();
        body.ShouldNotBeNull();
        body.Data.Assignment.RowVersion.ShouldBe(1);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Users.CountAsync(user => user.Id == body.Data.Id)).ShouldBe(1);
        (await context.UserRoleScopes.CountAsync(assignment => assignment.UserId == body.Data.Id)).ShouldBe(1);

        HttpResponseMessage fetched = await HttpClient.GetAsync($"admin/users/{body.Data.Id}/role-scope");
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<AssignmentData>? fetchedBody = await fetched.Content.ReadFromJsonAsync<ApiEnvelope<AssignmentData>>();
        fetchedBody!.Data.RowVersion.ShouldBe(1);
        fetchedBody.Data.RoleId.ShouldBe(WellKnownRoles.AdministratorId);
    }

    [Fact]
    public async Task Replace_ShouldIncrementVersion_AndRejectStaleState()
    {
        (Guid userId, _) = await RegisterAndLoginAsync();
        await AuthenticateAsAdministratorAsync();

        HttpResponseMessage updated = await HttpClient.PutAsJsonAsync($"admin/users/{userId}/role-scope", new
        {
            roleId = WellKnownRoles.WarehouseManagerId,
            scopeType = "Enterprise",
            scopeId = (Guid?)null,
            expectedRowVersion = 1
        });
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<AssignmentData>? updateBody = await updated.Content.ReadFromJsonAsync<ApiEnvelope<AssignmentData>>();
        updateBody!.Data.RowVersion.ShouldBe(2);

        HttpResponseMessage stale = await HttpClient.PutAsJsonAsync($"admin/users/{userId}/role-scope", new
        {
            roleId = WellKnownRoles.AuditorId,
            scopeType = "Enterprise",
            scopeId = (Guid?)null,
            expectedRowVersion = 1
        });
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ErrorEnvelope? error = await stale.Content.ReadFromJsonAsync<ErrorEnvelope>();
        error!.Error.Code.ShouldBe("USER_ROLE_SCOPES_ROW_VERSION_MISMATCH");
        error.Error.Details.GetProperty("user_id").GetGuid().ShouldBe(userId);
        error.Error.Details.GetProperty("expected_row_version").GetInt32().ShouldBe(1);
        error.Error.Details.GetProperty("current_row_version").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task CreateUser_ShouldRejectInvalidAssignments_WithoutCreatingUsers()
    {
        await AuthenticateAsAdministratorAsync();
        var organizationId = Guid.NewGuid();
        var inactiveSite = Site.Create(Guid.NewGuid(), organizationId, "Inactive", $"I-{Guid.NewGuid():N}"[..12], null);
        inactiveSite.SetStatus(Status.Inactive);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Organizations.Add(Organization.Create(organizationId, "Lifecycle Org", $"O-{Guid.NewGuid():N}"[..12]));
            context.Sites.Add(inactiveSite);
            await context.SaveChangesAsync();
        }

        (string Email, string Username, HttpResponseMessage Response)[] attempts =
        [
            await CreateInvalidAsync("OrganizationalUnit", Guid.NewGuid(), WellKnownRoles.AdministratorId),
            await CreateInvalidAsync("Enterprise", null, WellKnownRoles.WarehouseKeeperId),
            await CreateInvalidAsync("Site", inactiveSite.Id, WellKnownRoles.WarehouseManagerId)
        ];
        attempts.ShouldAllBe(attempt => !attempt.Response.IsSuccessStatusCode);
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach ((string email, string username, _) in attempts)
        {
            (await verify.Users.CountAsync(user => user.Email == email || user.Username == username)).ShouldBe(0);
        }
    }

    [Fact]
    public async Task CreateUser_ShouldRollbackUserWhenAssignmentPersistenceFailsAfterUserInsert()
    {
        await AuthenticateAsAdministratorAsync();
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"rollback-{suffix}@example.com";
        string username = $"rollback-{suffix}";

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER IF EXISTS "test_rollback_assignment_trigger" ON public.user_role_scopes;
                DROP FUNCTION IF EXISTS public."test_rollback_assignment"();
                CREATE OR REPLACE FUNCTION public."test_rollback_assignment"()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public.users WHERE id = NEW.user_id AND username LIKE 'rollback-%') THEN
                        RAISE EXCEPTION 'test-only assignment persistence failure';
                    END IF;
                    RETURN NEW;
                END;
                $fn$;
                CREATE TRIGGER "test_rollback_assignment_trigger"
                BEFORE INSERT ON public.user_role_scopes
                FOR EACH ROW EXECUTE FUNCTION public."test_rollback_assignment"();
                """);
        }

        try
        {
            HttpResponseMessage response = await HttpClient.PostAsJsonAsync("admin/users", new
            {
                email,
                username,
                firstName = "Rollback",
                lastName = "User",
                password = "Password123!",
                roleId = WellKnownRoles.AdministratorId,
                scopeType = "Enterprise",
                scopeId = (Guid?)null
            });
            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            string responseText = await response.Content.ReadAsStringAsync();
            responseText.ShouldNotContain("test-only assignment persistence failure");
            responseText.ShouldNotContain("Npgsql");
            responseText.ShouldNotContain("INSERT INTO");
            using (var document = JsonDocument.Parse(responseText))
            {
                document.RootElement.GetProperty("error").GetProperty("code").GetString().ShouldBe("SERVER_FAILURE");
                document.RootElement.GetProperty("error").GetProperty("message").GetString()
                    .ShouldBe("An unexpected server error occurred.");
            }

            await using AsyncServiceScope rollbackVerifyScope = factory.Services.CreateAsyncScope();
            ApplicationDbContext rollbackVerify = rollbackVerifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await rollbackVerify.Users.AnyAsync(user => user.Email == email || user.Username == username)).ShouldBeFalse();
        }
        finally
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER IF EXISTS "test_rollback_assignment_trigger" ON public.user_role_scopes;
                DROP FUNCTION IF EXISTS public."test_rollback_assignment"();
                """);
        }

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verify.Users.CountAsync(user => user.Email == email || user.Username == username)).ShouldBe(0);
        (await (from assignment in verify.UserRoleScopes
                join user in verify.Users on assignment.UserId equals user.Id
                where user.Email == email || user.Username == username
                select assignment).CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ConcurrentReplacements_WithSameVersion_ShouldHaveOneWinner()
    {
        (Guid userId, _) = await RegisterAndLoginAsync();
        await AuthenticateAsAdministratorAsync();
        Task<HttpResponseMessage> first = ReplaceAsync(userId, WellKnownRoles.WarehouseManagerId);
        Task<HttpResponseMessage> second = ReplaceAsync(userId, WellKnownRoles.AuditorId);
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.UserRoleScopes.CountAsync(assignment => assignment.UserId == userId)).ShouldBe(1);
        (await context.UserRoleScopes.SingleAsync(assignment => assignment.UserId == userId)).RowVersion.ShouldBe(2);
    }

    [Fact]
    public async Task LegacyUserWithoutAssignment_ShouldRequireExpectedVersionZero()
    {
        await AuthenticateAsAdministratorAsync();
        var userId = Guid.NewGuid();
        string email = UniqueEmail();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Users.Add(User.Create(userId, email, UsernameFor(email), "Legacy", "User", "hash"));
            await context.SaveChangesAsync();
        }

        HttpResponseMessage wrongVersion = await HttpClient.PutAsJsonAsync($"admin/users/{userId}/role-scope", new
        {
            roleId = WellKnownRoles.AdministratorId,
            scopeType = "Enterprise",
            scopeId = (Guid?)null,
            expectedRowVersion = 1
        });
        wrongVersion.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        HttpResponseMessage repaired = await HttpClient.PutAsJsonAsync($"admin/users/{userId}/role-scope", new
        {
            roleId = WellKnownRoles.AdministratorId,
            scopeType = "Enterprise",
            scopeId = (Guid?)null,
            expectedRowVersion = 0
        });
        repaired.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await repaired.Content.ReadFromJsonAsync<ApiEnvelope<AssignmentData>>())!.Data.RowVersion.ShouldBe(1);
    }

    [Fact]
    public async Task LegacyRoleScopeRoutes_ShouldNotBePubliclyWritableOrDeletable()
    {
        (Guid userId, _) = await RegisterAndLoginAsync();
        await AuthenticateAsAdministratorAsync();
        (await HttpClient.GetAsync($"admin/users/{userId}/role-scopes")).IsSuccessStatusCode.ShouldBeFalse();
        (await HttpClient.PostAsJsonAsync("admin/user-role-scopes", new { userId })).IsSuccessStatusCode.ShouldBeFalse();
        (await HttpClient.DeleteAsync($"admin/users/{userId}/role-scope")).IsSuccessStatusCode.ShouldBeFalse();
        (await HttpClient.DeleteAsync($"admin/user-role-scopes/{Guid.NewGuid()}")).IsSuccessStatusCode.ShouldBeFalse();
    }

    private async Task<HttpResponseMessage> ReplaceAsync(Guid userId, Guid roleId) =>
        await HttpClient.PutAsJsonAsync($"admin/users/{userId}/role-scope", new
        {
            roleId,
            scopeType = "Enterprise",
            scopeId = (Guid?)null,
            expectedRowVersion = 1
        });

    private async Task<(string Email, string Username, HttpResponseMessage Response)> CreateInvalidAsync(
        string scopeType, Guid? scopeId, Guid roleId)
    {
        string email = UniqueEmail();
        string username = UsernameFor(email);
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("admin/users", new
        {
            email,
            username,
            firstName = "Invalid",
            lastName = "Assignment",
            password = "Password123!",
            roleId,
            scopeType,
            scopeId
        });
        return (email, username, response);
    }

    private sealed record CreateUserData(Guid Id, AssignmentData Assignment);

    /// <summary>
    /// The assignment as it appears on the wire.
    /// <para>
    /// <paramref name="ScopeType"/> is a STRING, not the enum: the API serialises
    /// <c>UserAssignmentScopeType</c> through an explicit
    /// <c>JsonStringEnumConverter</c> because these responses are emitted via
    /// <c>Results.Ok</c>, which uses the minimal-API <c>JsonOptions</c> rather than
    /// the MVC options the global converter is registered on. Reading it as a bare
    /// enum here deserialised the name "Enterprise" through the default numeric
    /// <c>EnumConverter</c> and threw, which is what pinned the wire to a number.
    /// Asserting on the name is also the stronger check: it fails if the converter is
    /// ever dropped again.
    /// </para>
    /// </summary>
    private sealed record AssignmentData(Guid Id, Guid RoleId, string ScopeType, Guid? ScopeId, int RowVersion);
    private sealed record ErrorData(string Code, JsonElement Details);
    private sealed record ErrorEnvelope(bool Success, ErrorData Error);
}
