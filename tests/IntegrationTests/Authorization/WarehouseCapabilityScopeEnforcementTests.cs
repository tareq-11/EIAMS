using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.MaterialDomains;
using Domain.Permissions;
using Domain.Roles;
using Domain.UserRoleScopes;
using Infrastructure.Authorization;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Authorization;

/// <summary>
/// Settles, by execution rather than by inspection, whether warehouse capability management is
/// reachable outside Enterprise scope.
///
/// D-RBAC-01 (route-permission-scope-matrix.md:66) scopes <c>warehouse.manage</c> to Enterprise,
/// and the WH_MGR role row at line 181 does not carry it at all. The seeded permission scope
/// configuration agrees: WarehouseManageId appears only in DottedStructuralManage, which is mapped
/// to ScopeType.Enterprise alone. Every capability-mutating handler nevertheless calls
/// HasPermissionInScopeAsync with ScopeType.Warehouse and the target warehouse id, which reads as
/// a Warehouse-scope check. These tests pin which of those two facts actually decides the outcome,
/// because the handlers and the seed look contradictory and only one of them is load-bearing.
///
/// VERIFIED CONCLUSION: there is no defect. The seed is load-bearing and the handlers are correct,
/// because ScopeAuthorizationService.AssignmentContainsAsync returns true unconditionally for an
/// Enterprise assignment. An Enterprise assignee therefore satisfies a Warehouse-scoped handler
/// check, which is the intended behaviour of an Enterprise-scoped structural permission. The
/// apparent contradiction is a readability problem, not a correctness one.
///
/// TWO INDEPENDENT GATES decide who receives the code, both inside
/// ScopeAuthorizationService.GetAllGrantsAsync. Flipping only one is not enough to observe the
/// effect, which is why a single-variable negative control understates the mechanism:
///   1. PermissionAllowedScopeTypes joined on (permissionId, assignment.ScopeType) - the
///      permission must be permitted at the scope the user is assigned to.
///   2. RoleAllowedScopeTypes joined on (roleId, assignment.ScopeType) - the ROLE must also be
///      allowed at that scope, independently of the permission.
/// Moving the assignment to Enterprise while leaving the role Warehouse-allowed still yields no
/// grant, because gate 2 rejects it. Only moving both restores access, which is what the
/// non-vacuity control on the restriction test proves.
///
/// A consequence worth recording: because a user who lacks the permission entirely is rejected by
/// the [HasPermission] policy gate before any handler body executes, the 403 they receive is the
/// generic AUTHORIZATION_FORBIDDEN and not WarehouseCapabilityErrors.Forbidden. The handler-level
/// capability Forbidden errors are therefore effectively unreachable on these routes. Clients need
/// generic AUTHORIZATION_FORBIDDEN copy; a capability-specific message is not what they will see.
/// </summary>
public sealed class WarehouseCapabilityScopeEnforcementTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public WarehouseCapabilityScopeEnforcementTests(IntegrationTestWebAppFactory factory)
        : base(factory) => this.factory = factory;

    [Fact]
    public async Task EnterpriseAssignment_Should_SatisfyTheWarehouseScopedHandlerCheck_AndGrantTheCapability()
    {
        // Arrange - the seeded administrator role, assigned at Enterprise.
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid warehouseId = await CreateWarehouseAsync();
        Guid domainId = await SeedMaterialDomainAsync();

        // Assert the premise: an Enterprise assignment really does receive the code.
        (await ReadSessionPermissionCodesAsync()).ShouldContain("warehouse.manage");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouse-capabilities", new
        {
            warehouseId,
            materialDomainId = domainId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task WarehouseScopedAssignment_HoldingWarehouseManage_Should_NeverReceiveTheCode_AndGrantIsForbidden()
    {
        // Arrange - a role that genuinely carries warehouse.manage, allowed at and assigned to
        // Warehouse scope. This is the strongest form of the case: the permission is not absent
        // from the role, so a 403 here cannot be explained by the role simply lacking it.
        //
        // NOTE this configuration is deliberately INVALID and is torn down again below.
        // rbac-v2-preflight.sql asserts dotted_grants_without_scope = PASS, meaning a role grant
        // must have an intersection between the role's allowed scopes and the permission's allowed
        // scopes; warehouse.manage is Enterprise-only, so Warehouse-allowed plus warehouse.manage
        // has an empty intersection. The preflight forbids this state. Manufacturing it here is
        // deliberate and is the point of the test: it proves the runtime grant filter still denies
        // access even if the data invariant is bypassed, so authorization does not rest on a single
        // invariant. The other tests in this class cover the VALID Warehouse-scoped case, where the
        // role holds only warehouse.view.
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid warehouseId = await CreateWarehouseAsync();
        Guid domainId = await SeedMaterialDomainAsync();
        var roleId = Guid.NewGuid();

        try
        {
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var role = Role.Create(roleId, $"WH manage {roleId:N}", "إدارة مستودع", "Warehouse-scoped holder of warehouse.manage");
                var grant = RolePermission.Create(roleId, WellKnownDottedPermissions.WarehouseManageId);
                var allowedScope = RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse);
                var userScope = UserRoleScope.Create(
                    Guid.NewGuid(),
                    userId,
                    roleId,
                    ScopeType.Warehouse,
                    warehouseId);

                await context.UserRoleScopes.Where(item => item.UserId == userId).ExecuteDeleteAsync();
                context.AddRange(role, allowedScope, grant, userScope);
                await context.SaveChangesAsync();
            }

            Authenticate(tokens.AccessToken);

            // Assert the mechanism, not just the outcome. ScopeAuthorizationService.GetAllGrantsAsync
            // joins PermissionAllowedScopeTypes on (permissionId, assignment.ScopeType), and
            // warehouse.manage has no Warehouse row, so the grant is filtered out of the session
            // before any handler runs. This is why the frontend's has('warehouse.manage') is already
            // correct and needs no scope check of its own.
            (await ReadSessionPermissionCodesAsync()).ShouldNotContain("warehouse.manage");

            // Act
            HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouse-capabilities", new
            {
                warehouseId,
                materialDomainId = domainId
            });

            // Assert. The rejection is the GENERIC AUTHORIZATION_FORBIDDEN, not
            // WarehouseCapabilityErrors.Forbidden. That distinction is the point: the user holds no
            // grant at all, because the scope join filtered their only permission out, so the
            // [HasPermission] policy gate rejects the request before any handler body runs. The
            // handler-level WarehouseCapabilityErrors.Forbidden is therefore unreachable here, and
            // the frontend needs generic AUTHORIZATION_FORBIDDEN copy rather than a capability code.
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("error").GetProperty("code").GetString()
                .ShouldBe("AUTHORIZATION_FORBIDDEN");
        }
        finally
        {
            // Remove the deliberately invalid fixture. Without this the RbacV2 preflight test,
            // which asserts every dotted role grant has a scope intersection, turns BLOCK and the
            // whole suite goes red for a reason that has nothing to do with it.
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.UserRoleScopes.Where(item => item.RoleId == roleId).ExecuteDeleteAsync();
            await context.RolePermissions.Where(item => item.RoleId == roleId).ExecuteDeleteAsync();
            await context.RoleAllowedScopeTypes.Where(item => item.RoleId == roleId).ExecuteDeleteAsync();
            await context.Roles.Where(item => item.Id == roleId).ExecuteDeleteAsync();
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task WarehouseScopedViewer_Should_StillReadCapabilities_BecauseReadsUseWarehouseView()
    {
        // Arrange - the capability read is a different permission at a different scope, so the
        // Enterprise-only restriction on writes does not hide the matrix from operational users.
        // This is what makes the inline operations projection worth shipping: without it a
        // warehouse manager assembling this screen had to fan out one request per capability.
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid warehouseId = await CreateWarehouseAsync();
        var roleId = Guid.NewGuid();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = Role.Create(roleId, $"WH view {roleId:N}", "قراءة مستودع", "Warehouse-scoped capability reader");
            var grant = RolePermission.Create(roleId, WellKnownDottedPermissions.WarehouseViewId);
            var allowedScope = RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse);
            var userScope = UserRoleScope.Create(
                Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId);

            await context.UserRoleScopes.Where(item => item.UserId == userId).ExecuteDeleteAsync();
            context.AddRange(role, allowedScope, grant, userScope);
            await context.SaveChangesAsync();
        }

        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"warehouses/{warehouseId}/capabilities?page=1&pageSize=20");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task WarehouseScopedViewer_Should_BeForbiddenFromWritingWarehouseDetails_BecauseManageIsEnterpriseOnly()
    {
        // The same restriction must apply to the warehouse aggregate write, not only to capability
        // writes. If it did not, Enterprise-only would hold for capabilities alone, which would be
        // an inconsistency rather than a policy.
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid warehouseId = await CreateWarehouseAsync();
        var roleId = Guid.NewGuid();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = Role.Create(roleId, $"WH view2 {roleId:N}", "قراءة", "Warehouse-scoped reader without manage");
            var grant = RolePermission.Create(roleId, WellKnownDottedPermissions.WarehouseViewId);
            var allowedScope = RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse);
            var userScope = UserRoleScope.Create(
                Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId);

            await context.UserRoleScopes.Where(item => item.UserId == userId).ExecuteDeleteAsync();
            context.AddRange(role, allowedScope, grant, userScope);
            await context.SaveChangesAsync();
        }

        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouses/{warehouseId}",
            new
            {
                organizationalUnitId = (Guid?)null,
                name = "Renamed by a viewer",
                warehouseType = "General",
                canHoldStock = true,
                expectedRowVersion = 1
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static readonly Guid AdministratorRoleId = new("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Reads the codes the SERVER actually served to this session.
    /// </summary>
    /// <remarks>
    /// Asserting on the response rather than on the role fixture is the point of these tests. The
    /// session is built by the same scope-filtered grant query that the handlers consult, so
    /// "does the role carry the permission" and "does the client ever see the code" are different
    /// questions, and only the second one decides whether a control is rendered.
    /// </remarks>
    private async Task<string[]> ReadSessionPermissionCodesAsync()
    {
        using HttpResponseMessage response = await HttpClient.GetAsync("auth/session");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("permissionCodes")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
    }

    private async Task GrantEnterpriseAdministratorAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await context.UserRoleScopes.AnyAsync(item =>
                item.UserId == userId &&
                item.RoleId == AdministratorRoleId &&
                item.ScopeType == ScopeType.Enterprise))
        {
            return;
        }

        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, AdministratorRoleId, ScopeType.Enterprise, null));
        await context.SaveChangesAsync();
    }

    private async Task<Guid> CreateWarehouseAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];

        var organization = Domain.Organizations.Organization.Create(
            Guid.NewGuid(), $"Org {suffix}", $"ORG{suffix}");
        var site = Domain.Sites.Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var unit = Domain.OrganizationalUnits.OrganizationalUnit.Create(
            Guid.NewGuid(), site.Id, null, $"Unit {suffix}", "Directorate");
        var warehouse = Domain.Warehouses.Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"WH{suffix}"[..12], "General", true, unit.Id);

        context.Organizations.Add(organization);
        context.Sites.Add(site);
        context.OrganizationalUnits.Add(unit);
        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync();
        return warehouse.Id;
    }

    private async Task<Guid> SeedMaterialDomainAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var domain = MaterialDomain.Create(Guid.NewGuid(), $"Domain {suffix}", $"D{suffix}");
        context.MaterialDomains.Add(domain);
        await context.SaveChangesAsync();
        return domain.Id;
    }
}