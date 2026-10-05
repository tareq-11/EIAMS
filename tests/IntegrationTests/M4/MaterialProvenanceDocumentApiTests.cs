using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.Organizations;
using Domain.Permissions;
using Domain.ReceivingInfos;
using Domain.Roles;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.M4;

/// <summary>
/// 3B end-to-end gate: a draft line freezes the catalog state it was written against, and submit
/// refuses to reinterpret the document after the catalog moved. Runs against the real PostgreSQL
/// schema, so the provenance columns and their check constraints are exercised too.
/// </summary>
public sealed class MaterialProvenanceDocumentApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public MaterialProvenanceDocumentApiTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AddLine_Should_PersistSourceProvenance_WhenLineIsCreatedInAConvertedUnit()
    {
        // Arrange
        CatalogSeed seed = await SeedCatalogAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await AddLineAsync(seed, seed.SourceUnitId, 2m, seed.RowVersion);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid lineId = await ReadIdAsync(response);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DocumentLine line = await context.DocumentLines.AsNoTracking().SingleAsync(item => item.Id == lineId);
        line.SourceMaterialVersion.ShouldBe(1);
        line.SourceMaterialKind.ShouldBe(MaterialKind.Consumable);
        line.SourceTrackingType.ShouldBe(TrackingType.Quantity);
        line.SourceBaseUnitId.ShouldBe(seed.BaseUnitId);
        line.SourceConversionId.ShouldBe(seed.ConversionId);
        line.SourceConversionFactor.ShouldBe(12m);
        line.BaseQuantity.ShouldBe(24m);
    }

    [Fact]
    public async Task SubmitDocument_Should_ReturnConflict_WhenConversionFactorChangedAfterDraft()
    {
        // Arrange
        CatalogSeed seed = await SeedCatalogAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        Guid lineId = await ReadIdAsync(await AddLineAsync(seed, seed.SourceUnitId, 2m, seed.RowVersion));
        int rowVersion = await GetRowVersionAsync(seed.DocumentId);
        await ChangeConversionFactorAsync(seed.ConversionId, 15m);

        // Act
        HttpResponseMessage response = await SubmitAsync(seed.DocumentId, rowVersion);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadErrorCodeAsync(response)).ShouldBe("DOCUMENT_LINES_CONVERSION_PROVENANCE_CHANGED");
        (await GetStatusAsync(seed.DocumentId)).ShouldBe("Draft");
        (await GetLineSourceFactorAsync(lineId)).ShouldBe(12m);
    }

    [Fact]
    public async Task SubmitDocument_Should_ReturnConflict_WhenClassificationChangedAfterDraft()
    {
        // Arrange
        CatalogSeed seed = await SeedCatalogAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        await ReadIdAsync(await AddLineAsync(seed, seed.BaseUnitId, 5m, seed.RowVersion));
        int rowVersion = await GetRowVersionAsync(seed.DocumentId);
        await ChangeMaterialClassificationAsync(seed.MaterialId);

        // Act
        HttpResponseMessage response = await SubmitAsync(seed.DocumentId, rowVersion);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadErrorCodeAsync(response)).ShouldBe("DOCUMENT_LINES_MATERIAL_PROVENANCE_STALE");
        (await GetStatusAsync(seed.DocumentId)).ShouldBe("Draft");
    }

    [Fact]
    public async Task SubmitDocument_Should_FailClosed_WhenLegacyDraftLineHasNoProvenance()
    {
        // Arrange: emulate a draft line written before 3B. All provenance columns are deliberately
        // cleared, which the database accepts as the explicit legacy state; submit must not infer
        // its meaning from today's material catalog.
        CatalogSeed seed = await SeedCatalogAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        Guid lineId = await ReadIdAsync(await AddLineAsync(seed, seed.BaseUnitId, 5m, seed.RowVersion));
        int rowVersion = await GetRowVersionAsync(seed.DocumentId);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE public.document_lines
                SET source_material_version = NULL,
                    source_material_kind = NULL,
                    source_tracking_type = NULL,
                    source_base_unit_id = NULL
                WHERE id = {lineId}
                """);
        }

        // Act
        HttpResponseMessage response = await SubmitAsync(seed.DocumentId, rowVersion);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadErrorCodeAsync(response)).ShouldBe("DOCUMENT_LINES_PROVENANCE_NOT_CAPTURED");
        (await GetStatusAsync(seed.DocumentId)).ShouldBe("Draft");
    }

    [Fact]
    public async Task SubmitDocument_Should_Succeed_AfterLineReCapturesChangedProvenance()
    {
        // Arrange
        CatalogSeed seed = await SeedCatalogAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        Guid lineId = await ReadIdAsync(await AddLineAsync(seed, seed.SourceUnitId, 2m, seed.RowVersion));
        int rowVersion = await GetRowVersionAsync(seed.DocumentId);
        await ChangeConversionFactorAsync(seed.ConversionId, 15m);
        (await UpdateLineAsync(seed, lineId, 2m, seed.SourceUnitId, rowVersion)).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        // Act
        HttpResponseMessage response = await SubmitAsync(seed.DocumentId, await GetRowVersionAsync(seed.DocumentId));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await GetStatusAsync(seed.DocumentId)).ShouldBe("Submitted");
        (await GetLineBaseQuantityAsync(lineId)).ShouldBe(30m);
    }

    [Fact]
    public async Task PostDocument_Should_ReturnConflictAndWriteNoMovement_WhenConversionFactorChangedAfterSubmit()
    {
        // Arrange
        CatalogSeed seed = await SeedCatalogAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        Guid lineId = await ReadIdAsync(await AddLineAsync(seed, seed.SourceUnitId, 2m, seed.RowVersion));
        int rowVersion = await GetRowVersionAsync(seed.DocumentId);
        (await SubmitAsync(seed.DocumentId, rowVersion)).EnsureSuccessStatusCode();
        (await GetStatusAsync(seed.DocumentId)).ShouldBe("Submitted");
        // The command-side lock refuses this edit while the document is submitted, so the catalog
        // change is made out of band to prove the post path re-validates the captured snapshot
        // instead of trusting the live conversion.
        await ChangeConversionFactorAsync(seed.ConversionId, 15m);

        // Act
        HttpResponseMessage response = await PostAsync(seed.DocumentId, await GetRowVersionAsync(seed.DocumentId));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await ReadErrorCodeAsync(response)).ShouldBe("DOCUMENT_LINES_CONVERSION_PROVENANCE_CHANGED");
        (await GetStatusAsync(seed.DocumentId)).ShouldBe("Submitted");
        (await GetLineSourceFactorAsync(lineId)).ShouldBe(12m);
        (await CountStockMovementsAsync(seed.DocumentId)).ShouldBe(0);
    }

    [Fact]
    public async Task PostDocument_Should_Succeed_WhenCatalogIsUnchangedSinceSubmit()
    {
        // Arrange
        CatalogSeed seed = await SeedCatalogAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        Guid lineId = await ReadIdAsync(await AddLineAsync(seed, seed.SourceUnitId, 2m, seed.RowVersion));
        int rowVersion = await GetRowVersionAsync(seed.DocumentId);
        (await SubmitAsync(seed.DocumentId, rowVersion)).EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await PostAsync(seed.DocumentId, await GetRowVersionAsync(seed.DocumentId));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await GetStatusAsync(seed.DocumentId)).ShouldBe("Posted");
        (await GetLineBaseQuantityAsync(lineId)).ShouldBe(24m);
        (await CountStockMovementsAsync(seed.DocumentId)).ShouldBe(1);
    }

    [Fact]
    public async Task UpdateMaterial_Should_ReturnConflict_WhenClassificationChangesAfterDocumentIsSubmitted()
    {
        // Arrange
        CatalogSeed seed = await SeedCatalogAsync();
        (Guid documentUserId, AccessTokens documentTokens) = await RegisterAndLoginAsync();
        await GrantDocumentPermissionsAsync(documentUserId, seed.WarehouseId);
        Authenticate(documentTokens.AccessToken);
        Guid lineId = await ReadIdAsync(await AddLineAsync(seed, seed.BaseUnitId, 5m, seed.RowVersion));
        int rowVersion = await GetRowVersionAsync(seed.DocumentId);
        (await SubmitAsync(seed.DocumentId, rowVersion)).EnsureSuccessStatusCode();
        (await GetStatusAsync(seed.DocumentId)).ShouldBe("Submitted");
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantCatalogPermissionsAsync(userId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"catalog/materials/{seed.MaterialId}",
            new
            {
                nameAr = "مادة",
                nameEn = "Material",
                materialKind = "Asset",
                trackingType = "Serial",
                hasExpiry = false,
                attributes = (string?)null,
                expectedCatalogVersion = seed.CatalogVersion
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadErrorCodeAsync(response)).ShouldBe("MATERIALS_CLASSIFICATION_LOCKED");
        (await GetLineSourceKindAsync(lineId)).ShouldBe(MaterialKind.Consumable);
    }

    [Fact]
    public async Task UpdateMaterial_Should_RejectIntegerClassificationEnum()
    {
        // Arrange
        // Only catalog authority is exercised here, and registration already assigns the enterprise
        // administrator (which owns materials:manage). A second, warehouse-scoped assignment for the
        // same user is impossible under the one-assignment-per-user invariant, and unnecessary.
        CatalogSeed seed = await SeedCatalogAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantCatalogPermissionsAsync(userId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"catalog/materials/{seed.MaterialId}",
            new
            {
                nameAr = "مادة",
                nameEn = "Material",
                materialKind = 0,
                trackingType = 0,
                hasExpiry = false,
                attributes = (string?)null
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> AddLineAsync(
        CatalogSeed seed,
        Guid unitId,
        decimal quantity,
        int expectedRowVersion) =>
        await HttpClient.PostAsJsonAsync(
            $"warehouse-documents/{seed.DocumentId}/lines",
            new
            {
                materialId = seed.MaterialId,
                quantity,
                unitId,
                unitPrice = (decimal?)null,
                batchNumber = (string?)null,
                expiryDate = (DateOnly?)null,
                openingType = (string?)null,
                expectedRowVersion
            });

    private async Task<HttpResponseMessage> UpdateLineAsync(
        CatalogSeed seed,
        Guid lineId,
        decimal quantity,
        Guid unitId,
        int expectedRowVersion) =>
        await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{seed.DocumentId}/lines/{lineId}",
            new
            {
                quantity,
                unitId,
                unitPrice = (decimal?)null,
                batchNumber = (string?)null,
                expiryDate = (DateOnly?)null,
                openingType = (string?)null,
                expectedRowVersion
            });

    private async Task<HttpResponseMessage> SubmitAsync(Guid documentId, int expectedRowVersion) =>
        await HttpClient.PostAsJsonAsync(
            $"warehouse-documents/{documentId}/submit",
            new { expectedRowVersion });

    private async Task<HttpResponseMessage> PostAsync(Guid documentId, int expectedRowVersion) =>
        await HttpClient.PostAsJsonAsync(
            $"warehouse-documents/{documentId}/post",
            new { expectedRowVersion });

    private async Task<int> CountStockMovementsAsync(Guid documentId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.StockMovements.CountAsync(item => item.DocumentId == documentId);
    }

    private async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        using JsonDocument body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("error").GetProperty("code").GetString()!;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    private async Task<int> GetRowVersionAsync(Guid documentId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.WarehouseDocuments.Where(item => item.Id == documentId)
            .Select(item => item.RowVersion)
            .SingleAsync();
    }

    private async Task<string> GetStatusAsync(Guid documentId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DocumentStatus status = await context.WarehouseDocuments.Where(item => item.Id == documentId)
            .Select(item => item.DocumentStatus)
            .SingleAsync();
        return status.ToString();
    }

    private async Task<decimal?> GetLineSourceFactorAsync(Guid lineId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.DocumentLines.Where(item => item.Id == lineId)
            .Select(item => item.SourceConversionFactor)
            .SingleAsync();
    }

    private async Task<decimal> GetLineBaseQuantityAsync(Guid lineId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.DocumentLines.Where(item => item.Id == lineId)
            .Select(item => item.BaseQuantity)
            .SingleAsync();
    }

    private async Task<MaterialKind?> GetLineSourceKindAsync(Guid lineId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.DocumentLines.Where(item => item.Id == lineId)
            .Select(item => item.SourceMaterialKind)
            .SingleAsync();
    }

    private async Task ChangeConversionFactorAsync(Guid conversionId, decimal factor)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        MaterialUnitConversion conversion = await context.MaterialUnitConversions
            .SingleAsync(item => item.Id == conversionId);
        conversion.UpdateFactor(factor);
        await context.SaveChangesAsync();
    }

    private async Task ChangeMaterialClassificationAsync(Guid materialId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Material material = await context.Materials.SingleAsync(item => item.Id == materialId);
        material.UpdateDetails("مادة", "Material", MaterialKind.Durable, TrackingType.Serial, false, null);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Document lines are authorized at warehouse scope, so the role is scoped to the seeded
    /// warehouse; a user holds exactly one assignment, which is why the catalog flow below uses its
    /// own enterprise-scoped user.
    /// </summary>
    private async Task GrantDocumentPermissionsAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"3B document editor {roleId:N}", "دور اختباري", null));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        context.RolePermissions.AddRange(
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentUpdateId),
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentSubmitId),
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentPostId),
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentViewId));
        context.UserRoleScopes.RemoveRange(context.UserRoleScopes.Where(item => item.UserId == userId));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Material writes are authorized at enterprise scope, so the seeded administrator role is
    /// assigned at enterprise scope (the same pattern as the M2 catalog API tests). A user holds
    /// exactly one assignment (1C invariant, <c>ux_user_role_scopes_user_id</c>), so this replaces
    /// whatever the user already had instead of adding a second row beside it.
    /// </summary>
    private async Task GrantCatalogPermissionsAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.UserRoleScopes.RemoveRange(context.UserRoleScopes.Where(item => item.UserId == userId));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null));
        await context.SaveChangesAsync();
    }

    private async Task<CatalogSeed> SeedCatalogAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var warehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"W{suffix}", "General", true);
        var domain = MaterialDomain.Create(Guid.NewGuid(), $"Domain {suffix}", $"D{suffix}");
        var category = MaterialCategory.Create(
            Guid.NewGuid(), domain.Id, null, $"Category {suffix}", $"C{suffix}");
        var baseUnitId = Guid.NewGuid();
        var sourceUnitId = Guid.NewGuid();
        var family = MaterialFamily.Create(
            Guid.NewGuid(), category.Id, $"Family {suffix}", $"F{suffix}");
        var materialId = Guid.NewGuid();
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouse.Id, DocumentType.Receiving, $"REC3B-{suffix}");
        document.UpdatePaperReference($"P-{suffix}", 2026);
        // Posting a document requires a signed original, and it can only be set while the document is
        // still a draft, so it is attached as part of the seed and counted in the seeded row version.
        // The fixture's bootstrap administrator is the uploader of record: the attachment owner is a
        // foreign key, and the seed deliberately creates no user of its own.
        Guid attachmentUploaderId = await context.Users
            .OrderBy(user => user.CreatedAtUtc)
            .Select(user => user.Id)
            .FirstAsync(cancellationToken: CancellationToken.None);
        var attachmentId = Guid.NewGuid();

        context.AddRange(organization, site, warehouse, domain, category, family, document);
        context.ReceivingInfos.Add(ReceivingInfo.Create(
            document.Id, $"Supplier {suffix}", $"INV-{suffix}", ReceivingType.Supplier).Value);
        // Without the capability the post path is blocked before it ever reaches the line checks, so
        // the seeded warehouse must be allowed to receive the seeded material's domain.
        var capabilityId = Guid.NewGuid();
        context.WarehouseCapabilities.Add(WarehouseCapability.Create(capabilityId, warehouse.Id, domain.Id));
        context.WarehouseCapabilityOperations.Add(WarehouseCapabilityOperation.Create(
            Guid.NewGuid(), capabilityId, OperationType.Receiving));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(baseUnitId, "Piece", "pc", "Count"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(sourceUnitId, "Box", "box", "Count"));
        var material = Material.Create(
            materialId, family.Id, baseUnitId, $"مادة {suffix}", $"Material {suffix}", $"M{suffix}",
            MaterialKind.Consumable, TrackingType.Quantity, false, null);
        context.Materials.Add(material);
        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(), materialId, sourceUnitId, baseUnitId, 12m);
        context.MaterialUnitConversions.Add(conversion);
        // The document and the attachment reference each other, so the draft is written first and the
        // signed original is linked in a second save.
        await context.SaveChangesAsync();

        context.DocumentAttachments.Add(DocumentAttachment.Create(
            attachmentId,
            document.Id,
            AttachmentType.SignedOriginal,
            $"m4-tests/{suffix}.pdf",
            $"{suffix}.pdf",
            "application/pdf",
            100,
            suffix,
            attachmentUploaderId,
            DateTime.UtcNow));
        document.SetSignedCopy(attachmentId);
        await context.SaveChangesAsync();

        return new CatalogSeed(
            document.Id,
            document.RowVersion,
            warehouse.Id,
            materialId,
            material.CatalogVersion,
            baseUnitId,
            sourceUnitId,
            conversion.Id);
    }

    private sealed record CatalogSeed(
        Guid DocumentId,
        int RowVersion,
        Guid WarehouseId,
        Guid MaterialId,
        int CatalogVersion,
        Guid BaseUnitId,
        Guid SourceUnitId,
        Guid ConversionId);
}
