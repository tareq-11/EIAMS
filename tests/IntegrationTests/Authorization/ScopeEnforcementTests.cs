using System.Net;
using System.Net.Http.Json;
using Domain.Common;
using Domain.Permissions;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using IntegrationTests.Regression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Authorization;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ScopeEnforcementTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public ScopeEnforcementTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ProtectedEndpoint_Should_Return401_WhenNotAuthenticated()
    {
        // Act: Call protected endpoint without token
        HttpResponseMessage response = await HttpClient.GetAsync("organizations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuditEndpoint_Should_Return403_WhenUserLacksEnterpriseScope()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // Login as Warehouse Keeper (Warehouse scope, not Enterprise)
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Assign only Warehouse-level role
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userRole = UserRoleScope.Create(Guid.NewGuid(), userId, WellKnownRoles.WarehouseKeeperId, ScopeType.Warehouse, seed.WarehouseId);
            context.UserRoleScopes.Add(userRole);
            await context.SaveChangesAsync();
        }

        // 2. Act: Try to access Audit Logs endpoint (requires audit-logs:view at Enterprise scope)
        HttpResponseMessage response = await HttpClient.GetAsync($"audit-logs/{Guid.NewGuid()}");

        // 3. Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WarehouseScopedReadPermissions_Should_AuthorizeMatchingReadEndpointsOnly()
    {
        // Arrange
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        var roleId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        Guid capabilityId;

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = Role.Create(roleId, $"Read only {roleId:N}", "Warehouse read-only integration role");
            var warehouseGrant = RolePermission.Create(roleId, WellKnownPermissions.WarehousesViewId);
            var inventoryGrant = RolePermission.Create(roleId, WellKnownPermissions.InventoryViewId);
            var userScope = UserRoleScope.Create(
                Guid.NewGuid(),
                userId,
                roleId,
                ScopeType.Warehouse,
                seed.WarehouseId);
            var document = WarehouseDocument.CreateDraft(
                documentId,
                seed.WarehouseId,
                DocumentType.Receiving,
                $"READ-{Guid.NewGuid():N}"[..20]);

            capabilityId = await context.WarehouseCapabilities
                .Where(item => item.WarehouseId == seed.WarehouseId)
                .Select(item => item.Id)
                .SingleAsync();

            context.AddRange(role, warehouseGrant, inventoryGrant, userScope, document);
            await context.SaveChangesAsync();
        }

        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage listResponse = await HttpClient.GetAsync("warehouses");
        HttpResponseMessage byIdResponse = await HttpClient.GetAsync($"warehouses/{seed.WarehouseId}");
        HttpResponseMessage capabilitiesResponse = await HttpClient.GetAsync(
            $"warehouses/{seed.WarehouseId}/capabilities");
        HttpResponseMessage operationsResponse = await HttpClient.GetAsync(
            $"warehouse-capabilities/{capabilityId}/operations");
        HttpResponseMessage settingsResponse = await HttpClient.GetAsync(
            $"warehouses/{seed.WarehouseId}/material-settings");
        HttpResponseMessage balancesResponse = await HttpClient.GetAsync(
            $"warehouses/{seed.WarehouseId}/balances");
        HttpResponseMessage movementsResponse = await HttpClient.GetAsync(
            $"warehouses/{seed.WarehouseId}/stock-movements");
        HttpResponseMessage documentMovementsResponse = await HttpClient.GetAsync(
            $"warehouse-documents/{documentId}/stock-movements");
        HttpResponseMessage inaccessibleWarehouseResponse = await HttpClient.GetAsync(
            $"warehouses/{seed.DestinationWarehouseId}");
        HttpResponseMessage forbiddenUpdateResponse = await HttpClient.PutAsJsonAsync(
            $"warehouses/{seed.WarehouseId}",
            new
            {
                organizationalUnitId = seed.OrgUnitId,
                name = "Read-only write attempt",
                warehouseType = "Main",
                canHoldStock = true,
                expectedRowVersion = 1
            });

        // Assert
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<List<WarehouseListItem>>? list =
            await listResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<WarehouseListItem>>>();
        list.ShouldNotBeNull();
        list.Data.Select(item => item.Id).ShouldBe([seed.WarehouseId]);

        byIdResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        capabilitiesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        operationsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        settingsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        balancesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        movementsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        documentMovementsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        inaccessibleWarehouseResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        forbiddenUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await using AsyncServiceScope verificationScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Warehouse warehouse = await verificationContext.Warehouses.SingleAsync(item => item.Id == seed.WarehouseId);
        warehouse.Name.ShouldNotBe("Read-only write attempt");
        warehouse.RowVersion.ShouldBe(1);
    }

    [Fact]
    public async Task EnterpriseRolesViewPermission_Should_ReadAnotherUsersRoleScopes()
    {
        // Arrange
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        var roleId = Guid.NewGuid();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = Role.Create(roleId, $"Roles reader {roleId:N}", "Roles read-only integration role");
            var permission = RolePermission.Create(roleId, WellKnownPermissions.RolesViewId);
            var userScope = UserRoleScope.Create(
                Guid.NewGuid(),
                userId,
                roleId,
                ScopeType.Enterprise,
                null);
            context.AddRange(role, permission, userScope);
            await context.SaveChangesAsync();
        }

        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"admin/users/{seed.AdminUserId}/role-scopes");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed record WarehouseListItem(Guid Id);
}
