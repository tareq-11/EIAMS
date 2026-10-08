using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.MaterialDomains;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Sites;
using Domain.UserRoleScopes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.M2;

public sealed class WarehouseAndCapabilityApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;
    private static readonly Guid AdministratorRoleId = new("00000000-0000-0000-0000-000000000001");

    public WarehouseAndCapabilityApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task CreateWarehouse_Should_ReturnUnauthorized_WhenRequestHasNoToken()
    {
        // Arrange
        WarehouseParent parent = await SeedWarehouseParentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouses", new
        {
            siteId = parent.SiteId,
            organizationalUnitId = parent.OrganizationalUnitId,
            name = "Main",
            code = $"WH{Guid.NewGuid():N}",
            warehouseType = "General",
            canHoldStock = true
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateWarehouse_Should_CreateAndExposeWarehouse_WhenRequestIsAuthorized()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        WarehouseParent parent = await SeedWarehouseParentAsync();
        string code = $"WH{Guid.NewGuid():N}";

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouses", new
        {
            siteId = parent.SiteId,
            organizationalUnitId = parent.OrganizationalUnitId,
            name = "Main warehouse",
            code,
            warehouseType = "General",
            canHoldStock = true
        });
        Guid warehouseId = await ReadResourceIdAsync(response);
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"warehouses/{warehouseId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = await ReadJsonAsync(getResponse);
        body.RootElement.GetProperty("data").GetProperty("code").GetString().ShouldBe(code);
        body.RootElement.GetProperty("data").GetProperty("rowVersion").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task UpdateWarehouse_Should_PersistChangeAndIncrementRowVersion_WhenVersionMatches()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        WarehouseParent parent = await SeedWarehouseParentAsync();
        Guid warehouseId = await CreateWarehouseAsync(parent);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"warehouses/{warehouseId}", new
        {
            name = "Updated warehouse",
            organizationalUnitId = parent.OrganizationalUnitId,
            warehouseType = "Secure",
            canHoldStock = false,
            expectedRowVersion = 1
        });
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"warehouses/{warehouseId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = await ReadJsonAsync(getResponse);
        body.RootElement.GetProperty("data").GetProperty("name").GetString().ShouldBe("Updated warehouse");
        body.RootElement.GetProperty("data").GetProperty("canHoldStock").GetBoolean().ShouldBeFalse();
        body.RootElement.GetProperty("data").GetProperty("rowVersion").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task UpdateWarehouse_Should_ReturnConflict_WhenRowVersionIsStale()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        WarehouseParent parent = await SeedWarehouseParentAsync();
        Guid warehouseId = await CreateWarehouseAsync(parent);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"warehouses/{warehouseId}", new
        {
            name = "Stale update",
            organizationalUnitId = parent.OrganizationalUnitId,
            warehouseType = "General",
            canHoldStock = true,
            expectedRowVersion = 99
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("WAREHOUSES_ROW_VERSION_MISMATCH");
    }

    [Fact]
    public async Task GetWarehouse_Should_ReturnNotFound_WhenWarehouseDoesNotExist()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"warehouses/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GrantCapability_Should_CreateAndExposeCapability_WhenWarehouseAndDomainAreValid()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid warehouseId = await CreateWarehouseAsync(await SeedWarehouseParentAsync());
        Guid domainId = await SeedMaterialDomainAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouse-capabilities", new
        {
            warehouseId,
            materialDomainId = domainId
        });
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"warehouses/{warehouseId}/capabilities?page=1&pageSize=20");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = await ReadJsonAsync(getResponse);
        body.RootElement.GetProperty("data").GetArrayLength().ShouldBe(1);
        body.RootElement.GetProperty("data")[0].GetProperty("materialDomainId").GetGuid().ShouldBe(domainId);
    }

    [Fact]
    public async Task ListCapabilities_Should_ReturnEachCapabilityWithItsGrantedOperations_InDeclarationOrder()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid warehouseId = await CreateWarehouseAsync(await SeedWarehouseParentAsync());
        Guid domainId = await SeedMaterialDomainAsync();

        HttpResponseMessage grantResponse = await HttpClient.PostAsJsonAsync("warehouse-capabilities", new
        {
            warehouseId,
            materialDomainId = domainId
        });
        Guid capabilityId = await ReadResourceIdAsync(grantResponse);

        // Deliberately added out of declaration order. The projection promises the
        // OperationType declaration order, so an alphabetical (string-column) sort or an
        // unordered sequence group would fail here rather than pass by luck.
        foreach (OperationType operation in new[] { OperationType.Transfer, OperationType.Receiving })
        {
            HttpResponseMessage operationResponse = await HttpClient.PostAsJsonAsync(
                $"warehouse-capabilities/{capabilityId}/operations",
                new { operationType = (int)operation });
            operationResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        // Act
        HttpResponseMessage getResponse = await HttpClient.GetAsync(
            $"warehouses/{warehouseId}/capabilities?page=1&pageSize=20");

        // Assert
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = await ReadJsonAsync(getResponse);
        JsonElement capability = body.RootElement.GetProperty("data")[0];
        capability.GetProperty("id").GetGuid().ShouldBe(capabilityId);

        // Assert the exact set AND the order. Asserting only Count or only containment
        // would still pass against the pre-change behaviour if the array were empty and
        // the assertion were weak; enumerating the values is what makes this non-vacuous.
        capability.GetProperty("operations")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ShouldBe(["Receiving", "Transfer"]);
    }

    [Fact]
    public async Task ListCapabilities_Should_ReturnEmptyOperations_AndNotNull_WhenCapabilityHasNoOperations()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid warehouseId = await CreateWarehouseAsync(await SeedWarehouseParentAsync());
        Guid domainId = await SeedMaterialDomainAsync();

        await HttpClient.PostAsJsonAsync("warehouse-capabilities", new
        {
            warehouseId,
            materialDomainId = domainId
        });

        // Act
        HttpResponseMessage getResponse = await HttpClient.GetAsync(
            $"warehouses/{warehouseId}/capabilities?page=1&pageSize=20");

        // Assert
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = await ReadJsonAsync(getResponse);
        JsonElement operations = body.RootElement.GetProperty("data")[0].GetProperty("operations");

        // A capability with no operation rows must serialise as [] rather than null, because
        // clients bind this array directly. JsonValueKind distinguishes the two and a
        // .GetArrayLength() on null would throw rather than assert.
        operations.ValueKind.ShouldBe(JsonValueKind.Array);
        operations.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ListCapabilities_Should_AttachOperationsForEveryPageAndKeepPageSizeIndependentOfOperationCount()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid warehouseId = await CreateWarehouseAsync(await SeedWarehouseParentAsync());
        Guid domainId = await SeedMaterialDomainAsync();

        HttpResponseMessage grantResponse = await HttpClient.PostAsJsonAsync("warehouse-capabilities", new
        {
            warehouseId,
            materialDomainId = domainId
        });
        Guid capabilityId = await ReadResourceIdAsync(grantResponse);

        foreach (OperationType operation in Enum.GetValues<OperationType>())
        {
            await HttpClient.PostAsJsonAsync(
                $"warehouse-capabilities/{capabilityId}/operations",
                new { operationType = (int)operation });
        }

        // Act - one row on the page, carrying every operation
        HttpResponseMessage getResponse = await HttpClient.GetAsync(
            $"warehouses/{warehouseId}/capabilities?page=1&pageSize=20");

        // Assert
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = await ReadJsonAsync(getResponse);

        // The capability count is 1 even though the operation child table has 6 rows for it.
        // If the projection joined operations into the paged query instead of running a
        // second phase, this would read 6 and pagination would repeat and skip capabilities.
        body.RootElement.GetProperty("data").GetArrayLength().ShouldBe(1);
        body.RootElement.GetProperty("pagination").GetProperty("total_count").GetInt32().ShouldBe(1);

        // Every OperationType, in declaration order - Adjustment included, since it is a
        // real granted value and a projection that filtered or alphabetised would drop it.
        body.RootElement.GetProperty("data")[0].GetProperty("operations")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ShouldBe(["Receiving", "Issue", "Transfer", "Count", "Return", "Adjustment"]);
    }

    [Fact]
    public async Task GrantCapability_Should_ReturnConflict_WhenCapabilityAlreadyExists()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid warehouseId = await CreateWarehouseAsync(await SeedWarehouseParentAsync());
        Guid domainId = await SeedMaterialDomainAsync();
        HttpResponseMessage first = await HttpClient.PostAsJsonAsync("warehouse-capabilities", new
        {
            warehouseId,
            materialDomainId = domainId
        });
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouse-capabilities", new
        {
            warehouseId,
            materialDomainId = domainId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private async Task<Guid> CreateWarehouseAsync(WarehouseParent parent)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouses", new
        {
            siteId = parent.SiteId,
            organizationalUnitId = parent.OrganizationalUnitId,
            name = "Warehouse",
            code = $"WH{Guid.NewGuid():N}",
            warehouseType = "General",
            canHoldStock = true
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await ReadResourceIdAsync(response);
    }

    private async Task<WarehouseParent> SeedWarehouseParentAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var organizationalUnit = OrganizationalUnit.Create(
            Guid.NewGuid(), site.Id, null, $"Unit {suffix}", "Directorate");
        dbContext.Organizations.Add(organization);
        dbContext.Sites.Add(site);
        dbContext.OrganizationalUnits.Add(organizationalUnit);
        await dbContext.SaveChangesAsync();
        return new WarehouseParent(site.Id, organizationalUnit.Id);
    }

    private async Task<Guid> SeedMaterialDomainAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var domain = MaterialDomain.Create(Guid.NewGuid(), $"Domain {suffix}", $"D{suffix}");
        dbContext.MaterialDomains.Add(domain);
        await dbContext.SaveChangesAsync();
        return domain.Id;
    }

    private async Task GrantEnterpriseAdministratorAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await dbContext.UserRoleScopes.AnyAsync(scope =>
                scope.UserId == userId &&
                scope.RoleId == AdministratorRoleId &&
                scope.ScopeType == ScopeType.Enterprise))
        {
            return;
        }

        dbContext.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, AdministratorRoleId, ScopeType.Enterprise, null));
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> ReadResourceIdAsync(HttpResponseMessage response)
    {
        using JsonDocument body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    private sealed record WarehouseParent(Guid SiteId, Guid OrganizationalUnitId);

}
