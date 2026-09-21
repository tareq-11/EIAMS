using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.Organizations;
using Domain.Sites;
using Domain.UserRoleScopes;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.M3;

public sealed class DocumentPaperReferenceApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public DocumentPaperReferenceApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task UpdatePaperReference_Should_ReturnUnauthorized_WhenRequestHasNoToken()
    {
        // Arrange
        Guid documentId = await SeedDraftDocumentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/paper-reference",
            new { paperDocumentNumber = "P-1", paperDocumentYear = 2026, expectedRowVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePaperReference_Should_PersistAndExposeUpdatedReference_WhenVersionMatches()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid documentId = await SeedDraftDocumentAsync();
        Guid warehouseId = await GetDocumentWarehouseAsync(documentId);
        await GrantEnterpriseAdministratorAsync(userId, warehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/paper-reference",
            new { paperDocumentNumber = "P-2026-1", paperDocumentYear = 2026, expectedRowVersion = 1 });
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"warehouse-documents/{documentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = await ReadJsonAsync(getResponse);
        JsonElement data = body.RootElement.GetProperty("data");
        data.GetProperty("paperDocumentNumber").GetString().ShouldBe("P-2026-1");
        data.GetProperty("paperDocumentYear").GetInt32().ShouldBe(2026);
        data.GetProperty("rowVersion").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task UpdatePaperReference_Should_ReturnConflict_WhenRowVersionIsStale()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid documentId = await SeedDraftDocumentAsync();
        Guid warehouseId = await GetDocumentWarehouseAsync(documentId);
        await GrantEnterpriseAdministratorAsync(userId, warehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/paper-reference",
            new { paperDocumentNumber = "P-2026-1", paperDocumentYear = 2026, expectedRowVersion = 9 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("WAREHOUSE_DOCUMENTS_ROW_VERSION_MISMATCH");
    }

    [Fact]
    public async Task UpdatePaperReference_Should_ReturnNotFound_WhenDocumentDoesNotExist()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        var documentId = Guid.NewGuid();
        await GrantEnterpriseAdministratorAsync(userId, await SeedWarehouseForPermissionAsync());
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/paper-reference",
            new { paperDocumentNumber = "P-2026-1", paperDocumentYear = 2026, expectedRowVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<Guid> SeedDraftDocumentAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var warehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"W{suffix}", "General", true);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouse.Id, DocumentType.Receiving, $"REC-{suffix}");
        dbContext.AddRange(organization, site, warehouse, document);
        await dbContext.SaveChangesAsync();
        return document.Id;
    }

    private async Task<Guid> GetDocumentWarehouseAsync(Guid documentId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.WarehouseDocuments.Where(item => item.Id == documentId).Select(item => item.WarehouseId).SingleAsync();
    }

    private async Task<Guid> SeedWarehouseForPermissionAsync()
    {
        Guid documentId = await SeedDraftDocumentAsync();
        return await GetDocumentWarehouseAsync(documentId);
    }

    private async Task GrantEnterpriseAdministratorAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var roleId = Guid.NewGuid();
        dbContext.Roles.Add(Domain.Roles.Role.Create(roleId, $"M3 editor {roleId:N}", null));
        dbContext.RoleAllowedScopeTypes.Add(Domain.Roles.RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        dbContext.RolePermissions.AddRange(
            Domain.Roles.RolePermission.Create(roleId, Domain.Permissions.WellKnownDottedPermissions.DocumentUpdateId),
            Domain.Roles.RolePermission.Create(roleId, Domain.Permissions.WellKnownDottedPermissions.DocumentViewId));
        dbContext.UserRoleScopes.RemoveRange(dbContext.UserRoleScopes.Where(scope => scope.UserId == userId));
        dbContext.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await dbContext.SaveChangesAsync();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
}
