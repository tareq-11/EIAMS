using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Domain.Common;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.DocumentLines;
using Domain.DocumentAttachments;
using Domain.InventoryBalances;
using Domain.InventoryAdjustments;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.StockMovements;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Phase10;

[Collection(nameof(IntegrationTestCollection))]
public sealed class InventoryReadApiIntegrationTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public InventoryReadApiIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task GlobalInventoryReads_Should_RestrictEveryResultToAssignedWarehouse()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        InventoryReadSeed seed = await SeedInventoryAsync(userId);
        await GrantWarehouseReadPermissionsAsync(userId, seed.AllowedWarehouseId);
        Authenticate(tokens.AccessToken);

        // Act + Assert: global balance list contains only the assigned warehouse.
        HttpResponseMessage balancesResponse = await HttpClient.GetAsync("inventory/balances?page=1&pageSize=10");
        string balancesJson = await balancesResponse.Content.ReadAsStringAsync();
        balancesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, balancesJson);
        using var balancesBody = JsonDocument.Parse(balancesJson);
        JsonElement balances = balancesBody.RootElement.GetProperty("data");
        balances.GetArrayLength().ShouldBe(1);
        balances[0].GetProperty("warehouseId").GetGuid().ShouldBe(seed.AllowedWarehouseId);
        balancesBody.RootElement.GetProperty("pagination").GetProperty("total_items").GetInt32().ShouldBe(1);

        // A WarehouseId query parameter only narrows the authorized result; it cannot expand Scope.
        HttpResponseMessage outsideFilterResponse = await HttpClient.GetAsync(
            $"inventory/balances?warehouseId={seed.OutsideWarehouseId}&page=1&pageSize=10");
        outsideFilterResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var outsideFilterBody = JsonDocument.Parse(
            await outsideFilterResponse.Content.ReadAsStringAsync());
        outsideFilterBody.RootElement.GetProperty("data").GetArrayLength().ShouldBe(0);
        outsideFilterBody.RootElement.GetProperty("pagination").GetProperty("total_items").GetInt32().ShouldBe(0);

        // The same Scope rule is applied to both movement list and movement detail.
        HttpResponseMessage movementsResponse = await HttpClient.GetAsync(
            "inventory/movements?movementType=Receipt&page=1&pageSize=10");
        movementsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var movementsBody = JsonDocument.Parse(await movementsResponse.Content.ReadAsStringAsync());
        JsonElement movements = movementsBody.RootElement.GetProperty("data");
        movements.GetArrayLength().ShouldBe(1);
        movements[0].GetProperty("id").GetGuid().ShouldBe(seed.AllowedMovementId);
        movements[0].GetProperty("documentReferenceNumber").GetString().ShouldBe(seed.AllowedDocumentReference);

        HttpResponseMessage allowedDetailResponse = await HttpClient.GetAsync(
            $"inventory/movements/{seed.AllowedMovementId}");
        allowedDetailResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage outsideDetailResponse = await HttpClient.GetAsync(
            $"inventory/movements/{seed.OutsideMovementId}");
        outsideDetailResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        HttpResponseMessage allowedAttachments = await HttpClient.GetAsync(
            $"warehouse-documents/{seed.AllowedDocumentId}/attachments");
        allowedAttachments.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage outsideAttachments = await HttpClient.GetAsync(
            $"warehouse-documents/{seed.OutsideDocumentId}/attachments");
        outsideAttachments.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        HttpResponseMessage swappedAttachment = await HttpClient.GetAsync(
            $"warehouse-documents/{seed.AllowedDocumentId}/attachments/{seed.OutsideAttachmentId}/content");
        swappedAttachment.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var upload = new MultipartFormDataContent();
        using var file = new ByteArrayContent("%PDF-1.7\nscoped-test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        upload.Add(file, "File", "outside.pdf");
        upload.Add(new StringContent("SignedOriginal"), "AttachmentType");
        upload.Add(new StringContent("1"), "ExpectedRowVersion");
        HttpResponseMessage outsideUpload = await HttpClient.PostAsync(
            $"warehouse-documents/{seed.OutsideDocumentId}/attachments",
            upload);
        outsideUpload.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        HttpResponseMessage outsideDelete = await HttpClient.DeleteAsync(
            $"warehouse-documents/{seed.OutsideDocumentId}/attachments/{seed.OutsideAttachmentId}" +
            "?expectedRowVersion=1");
        outsideDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UiReadSurfaces_Should_ReturnOnlyScopedAdjustmentsAssetsAndReports()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        InventoryReadSeed seed = await SeedInventoryAsync(userId);
        await GrantWarehouseReadPermissionsAsync(userId, seed.AllowedWarehouseId);
        Authenticate(tokens.AccessToken);

        // Adjustments
        HttpResponseMessage adjustments = await HttpClient.GetAsync("adjustments?page=1&pageSize=10");
        adjustments.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var body = JsonDocument.Parse(await adjustments.Content.ReadAsStringAsync()))
        {
            body.RootElement.GetProperty("data").GetArrayLength().ShouldBe(1);
            body.RootElement.GetProperty("data")[0].GetProperty("id").GetGuid().ShouldBe(seed.AllowedAdjustmentId);
        }

        (await HttpClient.GetAsync($"adjustments/{seed.AllowedAdjustmentId}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await HttpClient.GetAsync($"adjustments/{seed.OutsideAdjustmentId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        HttpResponseMessage eligible = await HttpClient.GetAsync(
            "adjustments/disposal-eligible-assets?page=1&pageSize=10");
        eligible.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var body = JsonDocument.Parse(await eligible.Content.ReadAsStringAsync()))
        {
            body.RootElement.GetProperty("data").GetArrayLength().ShouldBe(1);
            body.RootElement.GetProperty("data")[0].GetProperty("id").GetGuid().ShouldBe(seed.AllowedAssetId);
        }

        // Assets
        HttpResponseMessage assets = await HttpClient.GetAsync("assets?page=1&pageSize=10");
        assets.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var body = JsonDocument.Parse(await assets.Content.ReadAsStringAsync()))
        {
            body.RootElement.GetProperty("data").GetArrayLength().ShouldBe(1);
            body.RootElement.GetProperty("data")[0].GetProperty("currentStatus").GetString().ShouldBe("InStock");
        }

        (await HttpClient.GetAsync($"assets/{seed.AllowedAssetId}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await HttpClient.GetAsync($"assets/{seed.OutsideAssetId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await HttpClient.GetAsync($"assets/{seed.AllowedAssetId}/movements?page=1&pageSize=10"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Reports use the same fixed Scope boundary.
        string[] reportRoutes =
        [
            "reports/dashboard",
            "reports/inventory?page=1&pageSize=10",
            "reports/assets?page=1&pageSize=10",
            "reports/documents?page=1&pageSize=10",
            "reports/count-adjustments?page=1&pageSize=10"
        ];
        foreach (string route in reportRoutes)
        {
            HttpResponseMessage response = await HttpClient.GetAsync(route);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task GlobalInventoryReads_Should_ReturnForbidden_WhenUserHasNoInventoryScopePermission()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantRoleWithoutInventoryPermissionAsync(userId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("inventory/balances?page=1&pageSize=10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<InventoryReadSeed> SeedInventoryAsync(Guid postedBy)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        DateTime now = DateTime.UtcNow;

        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"O{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var allowedWarehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Allowed {suffix}", $"A{suffix}", "General", true);
        var outsideWarehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Outside {suffix}", $"X{suffix}", "General", true);
        var unit = UnitOfMeasure.Create(Guid.NewGuid(), $"Piece {suffix}", $"P{suffix}", "Count");
        var domain = MaterialDomain.Create(Guid.NewGuid(), $"Domain {suffix}", $"D{suffix}");
        var category = MaterialCategory.Create(
            Guid.NewGuid(), domain.Id, null, $"Category {suffix}", $"C{suffix}");
        var family = MaterialFamily.Create(
            Guid.NewGuid(), category.Id, $"Family {suffix}", $"F{suffix}", unit.Id);
        var material = Material.Create(
            Guid.NewGuid(),
            family.Id,
            unit.Id,
            $"مادة {suffix}",
            $"Material {suffix}",
            $"M{suffix}",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            null);
        var assetMaterial = Material.Create(
            Guid.NewGuid(),
            family.Id,
            unit.Id,
            $"أصل {suffix}",
            $"Asset {suffix}",
            $"AS{suffix}",
            MaterialKind.Asset,
            TrackingType.Serial,
            false,
            null);

        string allowedReference = $"ALLOW-{suffix}";
        var allowedDocument = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), allowedWarehouse.Id, DocumentType.Receiving, allowedReference);
        var outsideDocument = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), outsideWarehouse.Id, DocumentType.Receiving, $"OUT-{suffix}");
        var allowedAttachment = DocumentAttachment.Create(
            Guid.NewGuid(), allowedDocument.Id, AttachmentType.SignedOriginal, $"allowed-{suffix}",
            "signed.pdf", "application/pdf", 1, $"checksum-a-{suffix}", postedBy, now);
        var outsideAttachment = DocumentAttachment.Create(
            Guid.NewGuid(), outsideDocument.Id, AttachmentType.SignedOriginal, $"outside-{suffix}",
            "signed.pdf", "application/pdf", 1, $"checksum-x-{suffix}", postedBy, now);
        Result<DocumentLine> allowedLine = DocumentLine.Create(
            Guid.NewGuid(), allowedDocument.Id, material.Id, DocumentLineType.Normal, 5m, unit.Id, 5m, null, null, null);
        Result<DocumentLine> outsideLine = DocumentLine.Create(
            Guid.NewGuid(), outsideDocument.Id, material.Id, DocumentLineType.Normal, 9m, unit.Id, 9m, null, null, null);
        Result<DocumentLine> allowedAssetLine = DocumentLine.Create(
            Guid.NewGuid(), allowedDocument.Id, assetMaterial.Id, DocumentLineType.Asset, 1m, unit.Id, 1m, null, null, null);
        Result<DocumentLine> outsideAssetLine = DocumentLine.Create(
            Guid.NewGuid(), outsideDocument.Id, assetMaterial.Id, DocumentLineType.Asset, 1m, unit.Id, 1m, null, null, null);
        allowedLine.IsSuccess.ShouldBeTrue();
        outsideLine.IsSuccess.ShouldBeTrue();
        allowedAssetLine.IsSuccess.ShouldBeTrue();
        outsideAssetLine.IsSuccess.ShouldBeTrue();

        var allowedBalance = InventoryBalance.CreateZero(
            Guid.NewGuid(), allowedWarehouse.Id, material.Id, now);
        var outsideBalance = InventoryBalance.CreateZero(
            Guid.NewGuid(), outsideWarehouse.Id, material.Id, now);
        allowedBalance.SetQuantity(5m, now).IsSuccess.ShouldBeTrue();
        outsideBalance.SetQuantity(9m, now).IsSuccess.ShouldBeTrue();

        Result<StockMovement> allowedMovement = StockMovement.Create(
            Guid.NewGuid(),
            allowedWarehouse.Id,
            material.Id,
            allowedDocument.Id,
            allowedLine.Value.Id,
            MovementType.Receipt,
            5m,
            postedBy,
            now);
        Result<StockMovement> outsideMovement = StockMovement.Create(
            Guid.NewGuid(),
            outsideWarehouse.Id,
            material.Id,
            outsideDocument.Id,
            outsideLine.Value.Id,
            MovementType.Receipt,
            9m,
            postedBy,
            now.AddMinutes(1));
        allowedMovement.IsSuccess.ShouldBeTrue();
        outsideMovement.IsSuccess.ShouldBeTrue();

        context.AddRange(
            organization,
            site,
            allowedWarehouse,
            outsideWarehouse,
            unit,
            domain,
            category,
            family,
            material,
            assetMaterial,
            allowedDocument,
            outsideDocument,
            allowedLine.Value,
            outsideLine.Value,
            allowedAssetLine.Value,
            outsideAssetLine.Value);
        await context.SaveChangesAsync();

        context.AddRange(allowedAttachment, outsideAttachment);
        await context.SaveChangesAsync();

        allowedDocument.UpdatePaperReference($"P-A-{suffix}", now.Year).IsSuccess.ShouldBeTrue();
        outsideDocument.UpdatePaperReference($"P-X-{suffix}", now.Year).IsSuccess.ShouldBeTrue();
        allowedDocument.SetSignedCopy(allowedAttachment.Id).IsSuccess.ShouldBeTrue();
        outsideDocument.SetSignedCopy(outsideAttachment.Id).IsSuccess.ShouldBeTrue();
        allowedDocument.Submit().IsSuccess.ShouldBeTrue();
        outsideDocument.Submit().IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();

        allowedDocument.MarkPosted(postedBy, now).IsSuccess.ShouldBeTrue();
        outsideDocument.MarkPosted(postedBy, now).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();

        Result<Asset> allowedAsset = Asset.CreateReceived(
            Guid.NewGuid(), assetMaterial.Id, allowedWarehouse.Id, allowedAssetLine.Value.Id,
            $"AA-{suffix}", DateOnly.FromDateTime(now), $"SA-{suffix}");
        Result<Asset> outsideAsset = Asset.CreateReceived(
            Guid.NewGuid(), assetMaterial.Id, outsideWarehouse.Id, outsideAssetLine.Value.Id,
            $"AX-{suffix}", DateOnly.FromDateTime(now), $"SX-{suffix}");
        allowedAsset.IsSuccess.ShouldBeTrue();
        outsideAsset.IsSuccess.ShouldBeTrue();
        Result<AssetMovementHistory> allowedAssetHistory = AssetMovementHistory.Create(
            Guid.NewGuid(), allowedAsset.Value.Id, allowedDocument.Id, AssetMovementType.Received, now);
        Result<AssetMovementHistory> outsideAssetHistory = AssetMovementHistory.Create(
            Guid.NewGuid(), outsideAsset.Value.Id, outsideDocument.Id, AssetMovementType.Received, now);
        allowedAssetHistory.IsSuccess.ShouldBeTrue();
        outsideAssetHistory.IsSuccess.ShouldBeTrue();

        var allowedAdjustmentDocument = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), allowedWarehouse.Id, DocumentType.Adjustment, $"ADJ-A-{suffix}");
        var outsideAdjustmentDocument = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), outsideWarehouse.Id, DocumentType.Adjustment, $"ADJ-X-{suffix}");
        Result<InventoryAdjustment> allowedAdjustment = InventoryAdjustment.Create(
            allowedAdjustmentDocument.Id, null, AdjustmentKind.Quantity, $"Reason {suffix}");
        Result<InventoryAdjustment> outsideAdjustment = InventoryAdjustment.Create(
            outsideAdjustmentDocument.Id, null, AdjustmentKind.Quantity, $"Outside {suffix}");
        allowedAdjustment.IsSuccess.ShouldBeTrue();
        outsideAdjustment.IsSuccess.ShouldBeTrue();
        Result<DocumentLine> allowedAdjustmentDocumentLine = DocumentLine.Create(
            Guid.NewGuid(), allowedAdjustmentDocument.Id, material.Id, DocumentLineType.Normal,
            2m, unit.Id, 2m, null, null, null);
        Result<DocumentLine> outsideAdjustmentDocumentLine = DocumentLine.Create(
            Guid.NewGuid(), outsideAdjustmentDocument.Id, material.Id, DocumentLineType.Normal,
            3m, unit.Id, 3m, null, null, null);
        allowedAdjustmentDocumentLine.IsSuccess.ShouldBeTrue();
        outsideAdjustmentDocumentLine.IsSuccess.ShouldBeTrue();
        Result<AdjustmentLine> allowedAdjustmentLine = AdjustmentLine.Create(
            allowedAdjustmentDocumentLine.Value.Id, allowedAdjustment.Value.Id, 2m, $"Reason {suffix}");
        Result<AdjustmentLine> outsideAdjustmentLine = AdjustmentLine.Create(
            outsideAdjustmentDocumentLine.Value.Id, outsideAdjustment.Value.Id, 3m, $"Outside {suffix}");
        allowedAdjustmentLine.IsSuccess.ShouldBeTrue();
        outsideAdjustmentLine.IsSuccess.ShouldBeTrue();

        context.AddRange(
            allowedAsset.Value,
            outsideAsset.Value,
            allowedAssetHistory.Value,
            outsideAssetHistory.Value,
            allowedAdjustmentDocument,
            outsideAdjustmentDocument,
            allowedAdjustment.Value,
            outsideAdjustment.Value,
            allowedAdjustmentDocumentLine.Value,
            outsideAdjustmentDocumentLine.Value,
            allowedAdjustmentLine.Value,
            outsideAdjustmentLine.Value);
        await context.SaveChangesAsync();

        context.AddRange(
            allowedBalance,
            outsideBalance,
            allowedMovement.Value,
            outsideMovement.Value);
        await context.SaveChangesAsync();

        return new InventoryReadSeed(
            allowedWarehouse.Id,
            outsideWarehouse.Id,
            allowedMovement.Value.Id,
            outsideMovement.Value.Id,
            allowedReference,
            allowedDocument.Id,
            outsideDocument.Id,
            outsideAttachment.Id,
            allowedAsset.Value.Id,
            outsideAsset.Value.Id,
            allowedAdjustment.Value.Id,
            outsideAdjustment.Value.Id);
    }

    private async Task GrantWarehouseReadPermissionsAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"InventoryViewer-{roleId:N}", null));
        context.RolePermissions.AddRange(
            RolePermission.Create(roleId, WellKnownPermissions.InventoryViewId),
            RolePermission.Create(roleId, WellKnownPermissions.AssetsViewId),
            RolePermission.Create(roleId, WellKnownPermissions.CustodiesViewId),
            RolePermission.Create(roleId, WellKnownPermissions.WarehouseDocumentsViewId),
            RolePermission.Create(roleId, WellKnownPermissions.WarehouseDocumentsEditId),
            RolePermission.Create(roleId, WellKnownPermissions.InventoryCountsViewId));

        UserRoleScope? assignment = await context.UserRoleScopes.SingleOrDefaultAsync(item => item.UserId == userId);
        if (assignment is null)
        {
            context.UserRoleScopes.Add(UserRoleScope.Create(
                Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        }
        else
        {
            assignment.ReplaceAssignment(roleId, ScopeType.Warehouse, warehouseId);
        }

        await context.SaveChangesAsync();
    }

    private async Task GrantRoleWithoutInventoryPermissionAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"NoInventoryAccess-{roleId:N}", null));

        UserRoleScope? assignment = await context.UserRoleScopes.SingleOrDefaultAsync(item => item.UserId == userId);
        if (assignment is null)
        {
            context.UserRoleScopes.Add(UserRoleScope.Create(
                Guid.NewGuid(), userId, roleId, ScopeType.Enterprise, null));
        }
        else
        {
            assignment.ReplaceAssignment(roleId, ScopeType.Enterprise, null);
        }

        await context.SaveChangesAsync();
    }

    private sealed record InventoryReadSeed(
        Guid AllowedWarehouseId,
        Guid OutsideWarehouseId,
        Guid AllowedMovementId,
        Guid OutsideMovementId,
        string AllowedDocumentReference,
        Guid AllowedDocumentId,
        Guid OutsideDocumentId,
        Guid OutsideAttachmentId,
        Guid AllowedAssetId,
        Guid OutsideAssetId,
        Guid AllowedAdjustmentId,
        Guid OutsideAdjustmentId);
}
