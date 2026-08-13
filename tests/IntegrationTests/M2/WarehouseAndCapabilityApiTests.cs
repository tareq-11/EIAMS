using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.MaterialDomains;
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
        Guid siteId = await SeedSiteAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouses", new
        {
            siteId,
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
        Guid siteId = await SeedSiteAsync();
        string code = $"WH{Guid.NewGuid():N}";

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouses", new
        {
            siteId,
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
        Guid warehouseId = await CreateWarehouseAsync(await SeedSiteAsync());

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"warehouses/{warehouseId}", new
        {
            name = "Updated warehouse",
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
        Guid warehouseId = await CreateWarehouseAsync(await SeedSiteAsync());

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"warehouses/{warehouseId}", new
        {
            name = "Stale update",
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
        Guid warehouseId = await CreateWarehouseAsync(await SeedSiteAsync());
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
    public async Task GrantCapability_Should_ReturnConflict_WhenCapabilityAlreadyExists()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid warehouseId = await CreateWarehouseAsync(await SeedSiteAsync());
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

    private async Task<Guid> CreateWarehouseAsync(Guid siteId)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("warehouses", new
        {
            siteId,
            name = "Warehouse",
            code = $"WH{Guid.NewGuid():N}",
            warehouseType = "General",
            canHoldStock = true
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await ReadResourceIdAsync(response);
    }

    private async Task<Guid> SeedSiteAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        dbContext.Organizations.Add(organization);
        dbContext.Sites.Add(site);
        await dbContext.SaveChangesAsync();
        return site.Id;
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

}
