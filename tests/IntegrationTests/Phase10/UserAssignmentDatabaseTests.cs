using Domain.Common;
using Domain.Organizations;
using Domain.Roles;
using Domain.Sites;
using Domain.Users;
using Domain.UserRoleScopes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace IntegrationTests.Phase10;

/// <summary>
/// The DATABASE half of D-SRS-01: cardinality enforced by PostgreSQL itself.
/// </summary>
/// <remarks>
/// WHY THIS FILE IS SEPARATE FROM <c>UserAssignmentLifecycleIntegrationTests</c>.
/// That suite proves the API behaves: create writes one assignment, replace
/// mutates one row, a stale version is refused, two concurrent replacements have
/// exactly one winner. Every one of those assertions would still hold if
/// <c>ux_user_role_scopes_user_id</c> were dropped — because each is driven through
/// a path that touches at most one row per user. That index is the ONLY thing
/// making "multiple assignments" unreachable, and it was the one D-SRS-01
/// mechanism with no test at all, while every other unique index in the system
/// has one (<c>M0M1AuthorizationAndDatabaseTests</c>: Organization, Site,
/// EmployeeNumber).
///
/// So this file bypasses the API deliberately and writes two assignment rows for
/// one user directly. Only the database can refuse that.
/// </remarks>
[Collection(nameof(IntegrationTestCollection))]
public sealed class UserAssignmentDatabaseTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public UserAssignmentDatabaseTests(IntegrationTestWebAppFactory factory) : base(factory) =>
        this.factory = factory;

    [Fact]
    public async Task Unique_Index_On_UserId_Should_Reject_A_Second_Assignment()
    {
        // Arrange: one real user holding the single assignment CreateUser would
        // give them, written through the DbContext precisely because the API
        // cannot express "give this user two assignments".
        (Guid userId, _) = await SeedUserWithOneAssignmentAsync();
        await using AsyncServiceScope seedScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext seedContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await seedContext.UserRoleScopes.CountAsync(a => a.UserId == userId)).ShouldBe(1);

        // Act: a second row for the same user, a different role at a different
        // (real, active) site — the exact shape a retired `Grant` handler would
        // have produced.
        await using AsyncServiceScope secondScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        secondContext.UserRoleScopes.Add(
            UserRoleScope.Create(
                Guid.NewGuid(),
                userId,
                WellKnownRoles.AuditorId,
                ScopeType.Site,
                await SeedSecondActiveSiteAsync()));

        // Assert: the DATABASE refuses it. A DbUpdateException is the unique index
        // talking; had the application been in the path it would have returned a
        // domain error instead, which is a different assertion entirely.
        DbUpdateException rejection = await Should.ThrowAsync<DbUpdateException>(
            async () => await secondContext.SaveChangesAsync(CancellationToken.None));
        rejection.InnerException!.Message.ShouldContain("ux_user_role_scopes_user_id");

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verify.UserRoleScopes.CountAsync(a => a.UserId == userId)).ShouldBe(1);
    }

    [Fact]
    public async Task Unique_Index_Should_Survive_Concurrent_Second_Assignment_Attempts()
    {
        // The sequential case above could still pass if the constraint were only
        // checked within a single transaction. PostgreSQL's unique index is
        // enforced across concurrent writers, and this is the test that says so:
        // two independent DbContexts racing to add a row for the same user must
        // end with exactly one, whatever the interleaving.
        (Guid userId, _) = await SeedUserWithOneAssignmentAsync();

        bool[] outcomes = await Task.WhenAll(
            TryAddAssignmentAsync(userId, WellKnownRoles.AuditorId),
            TryAddAssignmentAsync(userId, WellKnownRoles.WarehouseManagerId));

        // Neither may SUCCEED. Both failing is the expected outcome because the
        // seeded row already occupies the slot; what matters is that not one of
        // them slipped a second row in.
        outcomes.Count(succeeded => succeeded).ShouldBe(0);

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verify.UserRoleScopes.CountAsync(a => a.UserId == userId)).ShouldBe(1);
    }

    [Fact]
    public async Task Check_Constraint_Should_Reject_Enterprise_With_A_ScopeId()
    {
        // The second database-side guard, and the one that stops a user holding
        // an "Enterprise" assignment that quietly names a specific site — a
        // one-row assignment whose scope contradicts its own scope type.
        (Guid userId, _) = await SeedUserWithOneAssignmentAsync();
        Guid siteId = await SeedSecondActiveSiteAsync();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.UserRoleScopes.Add(
            UserRoleScope.Create(
                Guid.NewGuid(),
                userId,
                WellKnownRoles.AuditorId,
                ScopeType.Enterprise,
                siteId));

        DbUpdateException rejection = await Should.ThrowAsync<DbUpdateException>(
            async () => await context.SaveChangesAsync(CancellationToken.None));
        rejection.InnerException!.Message.ShouldContain("ck_user_role_scopes_scope_id");
    }

    /// <summary>
    /// Attempts a second assignment on its own connection and reports whether it
    /// was ACCEPTED, so the concurrent case can assert on the outcome rather
    /// than on which exception surfaced.
    /// </summary>
    private async Task<bool> TryAddAssignmentAsync(Guid userId, Guid roleId)
    {
        Guid siteId = await SeedSecondActiveSiteAsync();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.UserRoleScopes.Add(
            UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Site, siteId));
        try
        {
            await context.SaveChangesAsync(CancellationToken.None);
            return true;
        }
        catch (DbUpdateException)
        {
            // Expected. Returning false rather than swallowing keeps a genuine
            // failure from being reported as a successful refusal.
            return false;
        }
    }

    /// <summary>
    /// Seeds a user holding exactly one Site-scoped assignment, returning the
    /// user and the site that assignment names.
    /// </summary>
    private async Task<(Guid UserId, Guid SiteId)> SeedUserWithOneAssignmentAsync()
    {
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        string email = UniqueEmail();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Organizations.Add(
            Organization.Create(organizationId, "Cardinality Org", $"O-{Guid.NewGuid():N}"[..12]));
        context.Sites.Add(
            Site.Create(siteId, organizationId, "Cardinality", $"S-{Guid.NewGuid():N}"[..12], null));
        context.Users.Add(User.Create(userId, email, UsernameFor(email), "Cardinality", "User", "hash"));
        context.UserRoleScopes.Add(
            UserRoleScope.Create(
                Guid.NewGuid(), userId, WellKnownRoles.AdministratorId, ScopeType.Site, siteId));
        await context.SaveChangesAsync();
        return (userId, siteId);
    }

    /// <summary>
    /// A second real, active Site so the competing assignments reference a valid
    /// scope. A random Guid would fail the FOREIGN KEY first and mask the unique
    /// index entirely — the wrong exception still proves nothing about the right
    /// constraint.
    /// </summary>
    private async Task<Guid> SeedSecondActiveSiteAsync()
    {
        var siteId = Guid.NewGuid();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var organizationId = Guid.NewGuid();
        context.Organizations.Add(
            Organization.Create(organizationId, "Cardinality Org", $"O-{Guid.NewGuid():N}"[..12]));
        context.Sites.Add(
            Site.Create(siteId, organizationId, "Cardinality", $"S-{Guid.NewGuid():N}"[..12], null));
        await context.SaveChangesAsync();
        return siteId;
    }
}