using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authorization;
using Domain.Common;
using Domain.Employees;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.M0M1;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M0M1AuthorizationAndDatabaseTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public M0M1AuthorizationAndDatabaseTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task GetMaterials_Should_ReturnUnauthorizedEnvelope_WhenTokenIsMissing()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("catalog/materials");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeFalse();
        body.Error.Code.ShouldBe("AUTHENTICATION_REQUIRED");
    }

    [Fact]
    public async Task UpdateWarehouse_Should_DenySiteScopedMutationUnderLegacyPolicy()
    {
        // Arrange
        WarehouseSeed seed = await SeedWarehouseAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantPermissionAsync(userId, WellKnownDottedPermissions.WarehouseManageId, ScopeType.Site, seed.SiteId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouses/{seed.WarehouseId}",
            new
            {
                organizationalUnitId = seed.OrganizationalUnitId,
                name = "Updated warehouse",
                warehouseType = "Main",
                canHoldStock = true,
                expectedRowVersion = 1
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Warehouse warehouse = await context.Warehouses.SingleAsync(item => item.Id == seed.WarehouseId);
        warehouse.Name.ShouldNotBe("Updated warehouse");
        warehouse.RowVersion.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateWarehouse_Should_ReturnForbidden_WhenSiteGrantTargetsAnotherSite()
    {
        // Arrange
        WarehouseSeed seed = await SeedWarehouseAsync();
        Guid otherSiteId = await SeedSiteAsync(seed.OrganizationId);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantPermissionAsync(userId, WellKnownDottedPermissions.WarehouseManageId, ScopeType.Site, otherSiteId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouses/{seed.WarehouseId}",
            new
            {
                organizationalUnitId = seed.OrganizationalUnitId,
                name = "Should not update",
                warehouseType = "Main",
                canHoldStock = true,
                expectedRowVersion = 1
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Error.Code.ShouldBe("AUTHORIZATION_FORBIDDEN");
    }

    [Fact]
    public async Task ScopedWarehouseEditor_Should_NotPerformAdministratorUserMutation()
    {
        WarehouseSeed seed = await SeedWarehouseAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantPermissionAsync(userId, WellKnownDottedPermissions.WarehouseManageId, ScopeType.Warehouse, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("admin/users", new
        {
            email = $"scoped-editor-{Guid.NewGuid():N}@example.test",
            username = $"scoped-editor-{Guid.NewGuid():N}",
            firstName = "Scoped",
            lastName = "Editor",
            password = "Password123!",
            roleId = WellKnownRoles.AdministratorId,
            scopeType = "Enterprise",
            scopeId = (Guid?)null
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LegacyOrganizationalUnitAssignment_ShouldBeRejectedByCutoverConstraint()
    {
        WarehouseSeed seed = await SeedWarehouseAsync();
        var descendantWarehouseId = Guid.NewGuid();
        var siblingWarehouseId = Guid.NewGuid();
        var descendantUnitId = Guid.NewGuid();
        var siblingUnitId = Guid.NewGuid();

        await using (AsyncServiceScope setupScope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.OrganizationalUnits.AddRange(
                Domain.OrganizationalUnits.OrganizationalUnit.Create(descendantUnitId, seed.SiteId, seed.OrganizationalUnitId, "Descendant", "Department"),
                Domain.OrganizationalUnits.OrganizationalUnit.Create(siblingUnitId, seed.SiteId, null, "Sibling", "Department"));
            context.Warehouses.AddRange(
                Warehouse.Create(descendantWarehouseId, seed.SiteId, "Descendant warehouse", $"D-{descendantWarehouseId:N}"[..20], "Main", true, descendantUnitId),
                Warehouse.Create(siblingWarehouseId, seed.SiteId, "Sibling warehouse", $"S-{siblingWarehouseId:N}"[..20], "Main", true, siblingUnitId));
            await context.SaveChangesAsync();
        }

        (Guid userId, _) = await RegisterAndLoginAsync();
        await Should.ThrowAsync<DbUpdateException>(() =>
            GrantPermissionAsync(userId, WellKnownDottedPermissions.WarehouseManageId, ScopeType.OrganizationalUnit, seed.OrganizationalUnitId));
    }

    [Fact]
    public async Task CreateOrganization_Should_AuthorizeEnterpriseGrant()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantPermissionAsync(userId, WellKnownDottedPermissions.OrganizationManageId, ScopeType.Enterprise, null);
        Authenticate(tokens.AccessToken);
        string code = $"ORG-{Guid.NewGuid():N}";

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "organizations",
            new { name = "Enterprise organization", code });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<ResourceId>? body = await response.Content.ReadFromJsonAsync<ApiEnvelope<ResourceId>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Organizations.SingleAsync(item => item.Id == body.Data.Id)).Code.ShouldBe(code);
    }

    [Fact]
    public async Task RolePermissionEndpoint_Should_ReplaceTheCompleteSetInOneCall()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantPermissionsAsync(
            userId,
            ScopeType.Enterprise,
            null,
            WellKnownDottedPermissions.AdminRoleManageId,
            WellKnownDottedPermissions.AdminRoleViewId);
        Guid roleId = await SeedAssignableRoleAsync();
        Authenticate(tokens.AccessToken);

        // Act: replace with a set that both grants and revokes, since replacement is wholesale.
        HttpResponseMessage replaceResponse = await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}/permissions",
            new
            {
                permissionCodes = RolePermissionReplacementCodes.CatalogViewAndManage,
                expectedRowVersion = 1
            });
        HttpResponseMessage listedResponse = await HttpClient.GetAsync($"admin/roles/{roleId}/permissions");

        // Assert
        replaceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        listedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedApiEnvelope<PermissionItem>? listed = await listedResponse.Content.ReadFromJsonAsync<PagedApiEnvelope<PermissionItem>>();
        listed.ShouldNotBeNull();
        listed.Data.ShouldContain(item => item.Code == "catalog.manage");
        listed.Data.ShouldContain(item => item.Code == "catalog.view");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.RolePermissions.AnyAsync(item =>
            item.RoleId == roleId && item.PermissionId == WellKnownDottedPermissions.CatalogManageId)).ShouldBeTrue();
        (await context.RolePermissions.AnyAsync(item =>
            item.RoleId == roleId && item.PermissionId == WellKnownDottedPermissions.CatalogViewId)).ShouldBeTrue();
    }

    [Fact]
    public async Task RolePermissionEndpoint_Should_RevokeEverything_WhenReplacedWithAnEmptySet()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantPermissionsAsync(
            userId,
            ScopeType.Enterprise,
            null,
            WellKnownDottedPermissions.AdminRoleManageId,
            WellKnownDottedPermissions.AdminRoleViewId);
        Guid roleId = await SeedAssignableRoleAsync();
        Authenticate(tokens.AccessToken);

        (await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}/permissions",
            new
            {
                permissionCodes = RolePermissionReplacementCodes.CatalogManage,
                expectedRowVersion = 1
            })).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        HttpResponseMessage clearedResponse = await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}/permissions",
            new { permissionCodes = RolePermissionReplacementCodes.None, expectedRowVersion = 2 });

        // Assert
        clearedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.RolePermissions.AnyAsync(item => item.RoleId == roleId)).ShouldBeFalse();
    }

    [Fact]
    public async Task RetiredOneAtATimeRolePermissionEndpoints_Should_NotBeExposed()
    {
        // RESOLUTION-027 S23 retires the public one-at-a-time assign/remove endpoints in favour of
        // wholesale replacement. A 405 (route exists, verb not allowed) or 404 both satisfy this; what
        // must not happen is a successful grant or revoke through the old surface.
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantPermissionsAsync(
            userId,
            ScopeType.Enterprise,
            null,
            WellKnownDottedPermissions.AdminRoleManageId,
            WellKnownDottedPermissions.AdminRoleViewId);
        Guid roleId = await SeedAssignableRoleAsync();
        Authenticate(tokens.AccessToken);

        HttpResponseMessage assignResponse = await HttpClient.PostAsJsonAsync(
            $"admin/roles/{roleId}/permissions",
            new { permissionId = WellKnownDottedPermissions.CatalogManageId });
        HttpResponseMessage removeResponse = await HttpClient.DeleteAsync(
            $"admin/roles/{roleId}/permissions/{WellKnownDottedPermissions.CatalogManageId}");

        assignResponse.IsSuccessStatusCode.ShouldBeFalse();
        removeResponse.IsSuccessStatusCode.ShouldBeFalse();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.RolePermissions.AnyAsync(item => item.RoleId == roleId)).ShouldBeFalse();
    }

    [Fact]
    public async Task OrganizationCodeUniqueIndex_Should_RejectDuplicateCode()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string code = $"ORG-{Guid.NewGuid():N}";
        context.Organizations.Add(Organization.Create(Guid.NewGuid(), "First", code));
        await context.SaveChangesAsync();
        context.Organizations.Add(Organization.Create(Guid.NewGuid(), "Duplicate", code));

        // Act
        Task act() => context.SaveChangesAsync();

        // Assert
        await Should.ThrowAsync<DbUpdateException>(act);
    }

    [Fact]
    public async Task SiteCodeUniqueIndex_Should_RejectDuplicateCode()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var organizationId = Guid.NewGuid();
        string code = $"SITE-{Guid.NewGuid():N}";
        context.Organizations.Add(Organization.Create(organizationId, "Organization", $"ORG-{Guid.NewGuid():N}"));
        context.Sites.Add(Site.Create(Guid.NewGuid(), organizationId, "First", code, null));
        await context.SaveChangesAsync();
        context.Sites.Add(Site.Create(Guid.NewGuid(), organizationId, "Duplicate", code, null));

        // Act
        Task act() => context.SaveChangesAsync();

        // Assert
        await Should.ThrowAsync<DbUpdateException>(act);
    }

    [Fact]
    public async Task EmployeeNumberUniqueIndex_Should_RejectDuplicateNumber()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseSeed seed = await SeedWarehouseAsync(context);
        var unitId = Guid.NewGuid();
        string employeeNumber = $"EMP-{Guid.NewGuid():N}";
        context.OrganizationalUnits.Add(Domain.OrganizationalUnits.OrganizationalUnit.Create(
            unitId,
            seed.SiteId,
            null,
            "Finance",
            "Department"));
        context.Employees.Add(Employee.Create(Guid.NewGuid(), unitId, "First", employeeNumber, null));
        await context.SaveChangesAsync();
        context.Employees.Add(Employee.Create(Guid.NewGuid(), unitId, "Duplicate", employeeNumber, null));

        // Act
        Task act() => context.SaveChangesAsync();

        // Assert
        await Should.ThrowAsync<DbUpdateException>(act);
    }

    [Fact]
    public async Task UserRoleScopeCheckConstraint_Should_RejectEnterpriseScopeWithScopeId()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid userId = await SeedUserAsync(context);
        Guid roleId = await SeedRoleAsync(context);
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            userId,
            roleId,
            ScopeType.Enterprise,
            Guid.NewGuid()));

        // Act
        Task act() => context.SaveChangesAsync();

        // Assert
        await Should.ThrowAsync<DbUpdateException>(act);
    }

    [Fact]
    public async Task UserRoleScopePartialUniqueIndex_Should_RejectDuplicateEnterpriseGrant()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid userId = await SeedUserAsync(context);
        Guid roleId = await SeedRoleAsync(context);
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Enterprise, null));
        await context.SaveChangesAsync();
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Enterprise, null));

        // Act
        Task act() => context.SaveChangesAsync();

        // Assert
        await Should.ThrowAsync<DbUpdateException>(act);
    }

    [Fact]
    public async Task MaterialCodeUniqueIndex_Should_RejectDuplicateCode()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        MaterialSeed seed = await SeedMaterialAsync(context);
        string code = $"MAT-{Guid.NewGuid():N}";
        context.Materials.Add(Material.Create(
            Guid.NewGuid(),
            seed.FamilyId,
            seed.BaseUnitId,
            "المادة الأولى",
            null,
            code,
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            null));
        await context.SaveChangesAsync();
        context.Materials.Add(Material.Create(
            Guid.NewGuid(),
            seed.FamilyId,
            seed.BaseUnitId,
            "المادة الثانية",
            null,
            code,
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            null));

        // Act
        Task act() => context.SaveChangesAsync();

        // Assert
        await Should.ThrowAsync<DbUpdateException>(act);
    }

    [Fact]
    public async Task MaterialAttributesJsonbColumn_Should_RejectInvalidJson()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        MaterialSeed seed = await SeedMaterialAsync(context);
        context.Materials.Add(Material.Create(
            Guid.NewGuid(),
            seed.FamilyId,
            seed.BaseUnitId,
            "مادة غير صالحة",
            null,
            $"MAT-{Guid.NewGuid():N}",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            "not-json"));

        // Act
        Task invalidJson() => context.SaveChangesAsync();

        // Assert
        await Should.ThrowAsync<DbUpdateException>(invalidJson);

    }

    [Fact]
    public async Task MaterialUnitConversionPositiveFactorCheck_Should_RejectZero()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        MaterialSeed seed = await SeedMaterialAsync(context);
        context.MaterialUnitConversions.Add(MaterialUnitConversion.Create(
            Guid.NewGuid(),
            seed.MaterialId,
            seed.SourceUnitId,
            seed.BaseUnitId,
            0m));

        // Act
        Task act() => context.SaveChangesAsync();

        // Assert
        await Should.ThrowAsync<DbUpdateException>(act);
    }

    private async Task GrantPermissionAsync(Guid userId, Guid permissionId, ScopeType scopeType, Guid? scopeId)
    {
        await GrantPermissionsAsync(userId, scopeType, scopeId, permissionId);
    }

    private async Task GrantPermissionsAsync(
        Guid userId,
        ScopeType scopeType,
        Guid? scopeId,
        params Guid[] permissionIds)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.UserRoleScopes.Where(assignment => assignment.UserId == userId).ExecuteDeleteAsync();
        Guid roleId = await SeedRoleAsync(context);
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, scopeType));
        context.RolePermissions.AddRange(permissionIds.Select(permissionId =>
            RolePermission.Create(roleId, permissionId)));
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, roleId, scopeType, scopeId));
        await context.SaveChangesAsync();
    }

private static async Task<Guid> SeedRoleAsync(ApplicationDbContext context)
    {
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"Role-{roleId:N}", "دور اختباري", null));
        await context.SaveChangesAsync();
        return roleId;
    }

    /// <summary>
    /// A role that can actually be assigned somewhere. A role with no allowed scope types can never
    /// make any permission effective, so wholesale replacement correctly refuses every code for it;
    /// permission-replacement fixtures therefore need an assignable role.
    /// </summary>
    private async Task<Guid> SeedAssignableRoleAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid roleId = await SeedRoleAsync(context);
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Enterprise));
        await context.SaveChangesAsync();
        return roleId;
    }

    private async Task<WarehouseSeed> SeedWarehouseAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await SeedWarehouseAsync(context);
    }

    private static async Task<WarehouseSeed> SeedWarehouseAsync(ApplicationDbContext context)
    {
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var organizationalUnitId = Guid.NewGuid();
        context.Organizations.Add(Organization.Create(organizationId, $"Organization-{organizationId:N}", $"ORG-{organizationId:N}"));
        context.Sites.Add(Site.Create(siteId, organizationId, $"Site-{siteId:N}", $"SITE-{siteId:N}", null));
        context.OrganizationalUnits.Add(Domain.OrganizationalUnits.OrganizationalUnit.Create(
            organizationalUnitId, siteId, null, $"Unit-{organizationalUnitId:N}", "Directorate"));
        context.Warehouses.Add(Warehouse.Create(
            warehouseId,
            siteId,
            $"Warehouse-{warehouseId:N}",
            $"WH-{warehouseId:N}",
            "Main",
            true,
            organizationalUnitId));
        await context.SaveChangesAsync();
        return new WarehouseSeed(organizationId, siteId, organizationalUnitId, warehouseId);
    }

    private async Task<Guid> SeedSiteAsync(Guid organizationId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var siteId = Guid.NewGuid();
        context.Sites.Add(Site.Create(siteId, organizationId, $"Site-{siteId:N}", $"SITE-{siteId:N}", null));
        await context.SaveChangesAsync();
        return siteId;
    }

    private static async Task<Guid> SeedUserAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        context.Users.Add(Domain.Users.User.Create(userId, $"user-{userId:N}@example.com", "User", "Test", "hash"));
        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<MaterialSeed> SeedMaterialAsync(ApplicationDbContext context)
    {
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();
        var sourceUnitId = Guid.NewGuid();
        context.MaterialDomains.Add(MaterialDomain.Create(domainId, $"Domain-{domainId:N}", $"DOM-{domainId:N}"));
        context.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, $"Category-{categoryId:N}", $"CAT-{categoryId:N}"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(baseUnitId, "Piece", "pc", "Count"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(sourceUnitId, "Box", "box", "Count"));
        context.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, $"Family-{familyId:N}", $"FAM-{familyId:N}"));
        context.Materials.Add(Material.Create(
            materialId,
            familyId,
            baseUnitId,
            "مادة",
            "Material",
            $"MAT-{materialId:N}",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            "{}"));
        await context.SaveChangesAsync();
        return new MaterialSeed(familyId, materialId, baseUnitId, sourceUnitId);
    }

    private sealed record WarehouseSeed(
        Guid OrganizationId,
        Guid SiteId,
        Guid OrganizationalUnitId,
        Guid WarehouseId);

    private sealed record MaterialSeed(Guid FamilyId, Guid MaterialId, Guid BaseUnitId, Guid SourceUnitId);

    private sealed record ResourceId(Guid Id);

    private sealed record PermissionItem(Guid Id, string Code);

    private sealed record PagedApiEnvelope<T>(bool Success, IReadOnlyList<T> Data);

    private sealed record ApiErrorEnvelope(bool Success, ApiErrorItem Error);

    private sealed record ApiErrorItem(string Code);
}
