using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.ExternalParties;
using Domain.Organizations;
using Domain.Sites;
using Domain.UserRoleScopes;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.M4;

public sealed class ReceivingInfoApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public ReceivingInfoApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_PersistAndExposeDetails_WhenDocumentIsDraftReceiving()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid documentId = await SeedDocumentAsync(DocumentType.Receiving, submitted: false);
        await GrantEnterpriseAdministratorAsync(userId, await GetDocumentWarehouseAsync(documentId));
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await PutReceivingInfoAsync(documentId, expectedRowVersion: 1);
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"warehouse-documents/{documentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = await ReadJsonAsync(getResponse);
        JsonElement receivingInfo = body.RootElement.GetProperty("data").GetProperty("receivingInfo");
        receivingInfo.GetProperty("supplierRef").GetString().ShouldStartWith("SUP-");
        receivingInfo.GetProperty("supplierInvoiceRef").GetString().ShouldBe("INV-1");
        receivingInfo.GetProperty("receivingType").GetString().ShouldBe("Supplier");
        body.RootElement.GetProperty("data").GetProperty("rowVersion").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_ReturnConflict_WhenRowVersionIsStale()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid documentId = await SeedDocumentAsync(DocumentType.Receiving, submitted: false);
        await GrantEnterpriseAdministratorAsync(userId, await GetDocumentWarehouseAsync(documentId));
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await PutReceivingInfoAsync(documentId, expectedRowVersion: 99);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("WAREHOUSE_DOCUMENTS_ROW_VERSION_MISMATCH");
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_ReturnBadRequest_WhenDocumentIsNotReceiving()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid documentId = await SeedDocumentAsync(DocumentType.Issue, submitted: false);
        await GrantEnterpriseAdministratorAsync(userId, await GetDocumentWarehouseAsync(documentId));
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await PutReceivingInfoAsync(documentId, expectedRowVersion: 1);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("RECEIVING_INFO_WRONG_DOCUMENT_TYPE");
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_RejectTransferAsReceivingType()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid documentId = await SeedDocumentAsync(DocumentType.Receiving, submitted: false);
        await GrantEnterpriseAdministratorAsync(userId, await GetDocumentWarehouseAsync(documentId));
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await PutReceivingInfoAsync(
            documentId, expectedRowVersion: 1, receivingType: "Transfer");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.ReceivingInfos.AnyAsync(item => item.Id == documentId)).ShouldBeFalse();
        (await dbContext.WarehouseDocuments.Where(item => item.Id == documentId)
            .Select(item => item.RowVersion).SingleAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_RejectInternalSiteAsSupplier()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid documentId = await SeedDocumentAsync(DocumentType.Receiving, submitted: false);
        Guid warehouseId = await GetDocumentWarehouseAsync(documentId);
        Guid siteId = await GetDocumentSiteAsync(warehouseId);
        await GrantEnterpriseAdministratorAsync(userId, warehouseId);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/receiving-info",
            new { supplierPartyId = siteId, supplierInvoiceRef = "INV-1", receivingType = "Supplier", expectedRowVersion = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("RECEIVING_INFO_SUPPLIER_PARTY_NOT_FOUND");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.ReceivingInfos.AnyAsync(info => info.Id == documentId)).ShouldBeFalse();
        (await context.WarehouseDocuments.Where(item => item.Id == documentId)
            .Select(item => item.RowVersion).SingleAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_RejectInactiveExternalSupplier()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid documentId = await SeedDocumentAsync(DocumentType.Receiving, submitted: false);
        Guid warehouseId = await GetDocumentWarehouseAsync(documentId);
        Guid supplierId = await CreateSupplierAsync();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ExternalParty supplier = await context.ExternalParties.SingleAsync(item => item.Id == supplierId);
            supplier.SetStatus(Status.Inactive);
            await context.SaveChangesAsync();
        }
        await GrantEnterpriseAdministratorAsync(userId, warehouseId);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/receiving-info",
            new { supplierPartyId = supplierId, supplierInvoiceRef = "INV-1", receivingType = "Supplier", expectedRowVersion = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyContext.ReceivingInfos.AnyAsync(info => info.Id == documentId)).ShouldBeFalse();
    }

    [Fact]
    public async Task ReceivingSupplierRef_ShouldRemainASnapshotUntilReceivingInfoIsExplicitlyUpdated()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid documentId = await SeedDocumentAsync(DocumentType.Receiving, submitted: false);
        Guid warehouseId = await GetDocumentWarehouseAsync(documentId);
        Guid supplierId = await CreateSupplierAsync();
        string originalCode;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            originalCode = await context.ExternalParties.Where(item => item.Id == supplierId)
                .Select(item => item.Code!).SingleAsync();
        }
        await GrantEnterpriseAdministratorAsync(userId, warehouseId);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage initialUpsert = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/receiving-info",
            new { supplierPartyId = supplierId, supplierInvoiceRef = "INV-1", receivingType = "Supplier", expectedRowVersion = 1 });
        initialUpsert.EnsureSuccessStatusCode();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ExternalParty supplier = await context.ExternalParties.SingleAsync(item => item.Id == supplierId);
            supplier.UpdateDetails("Updated supplier", "SUP-UPDATED", null, null);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage readAfterCatalogChange = await HttpClient.GetAsync($"warehouse-documents/{documentId}");
        readAfterCatalogChange.EnsureSuccessStatusCode();
        using JsonDocument oldSnapshotBody = await ReadJsonAsync(readAfterCatalogChange);
        oldSnapshotBody.RootElement.GetProperty("data").GetProperty("receivingInfo")
            .GetProperty("supplierRef").GetString().ShouldBe(originalCode);

        HttpResponseMessage refreshedUpsert = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/receiving-info",
            new { supplierPartyId = supplierId, supplierInvoiceRef = "INV-1", receivingType = "Supplier", expectedRowVersion = 2 });
        refreshedUpsert.EnsureSuccessStatusCode();

        HttpResponseMessage readAfterExplicitUpdate = await HttpClient.GetAsync($"warehouse-documents/{documentId}");
        readAfterExplicitUpdate.EnsureSuccessStatusCode();
        using JsonDocument newSnapshotBody = await ReadJsonAsync(readAfterExplicitUpdate);
        newSnapshotBody.RootElement.GetProperty("data").GetProperty("receivingInfo")
            .GetProperty("supplierRef").GetString().ShouldBe("SUP-UPDATED");
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_ReturnBadRequest_WhenDocumentIsSubmitted()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid documentId = await SeedDocumentAsync(DocumentType.Receiving, submitted: true);
        await GrantEnterpriseAdministratorAsync(userId, await GetDocumentWarehouseAsync(documentId));
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await PutReceivingInfoAsync(documentId, expectedRowVersion: 3);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("WAREHOUSE_DOCUMENTS_NOT_EDITABLE");
    }

    private async Task<HttpResponseMessage> PutReceivingInfoAsync(
        Guid documentId,
        int expectedRowVersion,
        string receivingType = "Supplier")
    {
        Guid supplierPartyId = await CreateSupplierAsync();
        return await HttpClient.PutAsJsonAsync($"warehouse-documents/{documentId}/receiving-info", new
        {
            supplierPartyId,
            supplierInvoiceRef = "INV-1",
            receivingType,
            expectedRowVersion
        });
    }

    private async Task<Guid> CreateSupplierAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var supplier = ExternalParty.Create(Guid.NewGuid(), $"Supplier {suffix}", $"SUP-{suffix}", null, null);
        dbContext.ExternalParties.Add(supplier);
        await dbContext.SaveChangesAsync();
        return supplier.Id;
    }

    private async Task<Guid> SeedDocumentAsync(DocumentType documentType, bool submitted)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var warehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"W{suffix}", "General", true);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouse.Id, documentType, $"DOC-{suffix}");

        if (submitted)
        {
            document.UpdatePaperReference("P-1", 2026).IsSuccess.ShouldBeTrue();
            document.Submit().IsSuccess.ShouldBeTrue();
        }

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

    private async Task<Guid> GetDocumentSiteAsync(Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.Warehouses.Where(item => item.Id == warehouseId)
            .Select(item => item.SiteId).SingleAsync();
    }

    private async Task GrantEnterpriseAdministratorAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var roleId = Guid.NewGuid();
        dbContext.Roles.Add(Domain.Roles.Role.Create(roleId, $"M4 editor {roleId:N}", null));
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
