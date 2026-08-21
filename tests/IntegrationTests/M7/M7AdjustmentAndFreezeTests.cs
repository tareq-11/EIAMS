using Application.Abstractions.Posting;
using Domain.Common;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.InventoryCounts;
using Domain.IssueTos;
using Domain.Custodies;
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
using Domain.Users;
using Domain.UserRoleScopes;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Net;
using SharedKernel;

namespace IntegrationTests.M7;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M7AdjustmentAndFreezeTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public M7AdjustmentAndFreezeTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task QuantityAdjustment_Should_PostSignedDifference_AndUpdateBalance()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, -2m);

        // Act
        Result<Guid> result = await PostAsync(adjustment.Id, adjustment.RowVersion, seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        decimal balance = await dbContext.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.MaterialId)
            .Select(item => item.Quantity).SingleAsync();
        balance.ShouldBe(3m);
        StockMovement movement = await dbContext.StockMovements.SingleAsync(item => item.DocumentId == adjustment.Id);
        movement.MovementType.ShouldBe(MovementType.AdjustmentOut);
        movement.QuantityDelta.ShouldBe(-2m);
        (await dbContext.InventoryAdjustments.SingleAsync(item => item.Id == adjustment.Id)).Status
            .ShouldBe(InventoryAdjustmentStatus.Posted);
    }

    [Fact]
    public async Task HardFreeze_Should_BlockIntersectingPosting_WithoutPartialLedgerChanges()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, -1m);
        await StartHardFreezeAsync(seed);

        // Act
        Result<Guid> result = await PostAsync(adjustment.Id, adjustment.RowVersion, seed.UserId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("InventoryCounts.PostingBlocked");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == adjustment.Id)).ShouldBeFalse();
        (await dbContext.WarehouseDocuments.SingleAsync(item => item.Id == adjustment.Id)).DocumentStatus
            .ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public async Task NoFreeze_Should_NotBlockPosting()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, 1m);
        await StartCountAsync(seed, FreezePolicy.NoFreeze);

        // Act
        Result<PostingOutcome> result = await PostWithOutcomeAsync(
            adjustment.Id,
            adjustment.RowVersion,
            seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task SoftFreeze_Should_PostAndReturnObservableWarning()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, 1m);
        await StartCountAsync(seed, FreezePolicy.SoftFreeze);

        // Act
        Result<PostingOutcome> result = await PostWithOutcomeAsync(
            adjustment.Id,
            adjustment.RowVersion,
            seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        PostingWarning warning = result.Value.Warnings.ShouldHaveSingleItem();
        warning.Code.ShouldBe("InventoryCounts.SoftFreezeActive");
        warning.WarehouseId.ShouldBe(seed.WarehouseId);
    }

    [Fact]
    public async Task FreezeStatus_Should_ReturnSoftFreezeThroughHttpEnvelope()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await StartCountAsync(seed, FreezePolicy.SoftFreeze);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantInventoryCountViewAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"warehouses/{seed.WarehouseId}/inventory-freeze-status");

        // Assert
        response.EnsureSuccessStatusCode();
        ApiEnvelope<FreezeStatusDto>? envelope =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<FreezeStatusDto>>();
        envelope.ShouldNotBeNull();
        envelope.Success.ShouldBeTrue();
        envelope.Data.IsPostingBlocked.ShouldBeFalse();
        envelope.Data.HasSoftFreezeWarning.ShouldBeTrue();
        envelope.Data.ActiveCounts.ShouldHaveSingleItem().FreezePolicy.ShouldBe(FreezePolicy.SoftFreeze);
    }

    [Fact]
    public async Task PostDocument_Should_ReturnSoftFreezeWarningThroughHttpEnvelope()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, 1m);
        await StartCountAsync(seed, FreezePolicy.SoftFreeze);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantInventoryCountViewAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"warehouse-documents/{adjustment.Id}/post",
            new { expectedRowVersion = adjustment.RowVersion });

        // Assert
        response.EnsureSuccessStatusCode();
        ApiEnvelope<PostDocumentDto>? envelope =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<PostDocumentDto>>();
        envelope.ShouldNotBeNull();
        envelope.Success.ShouldBeTrue();
        envelope.Data.DocumentId.ShouldBe(adjustment.Id);
        PostingWarningDto warning = envelope.Data.Warnings.ShouldHaveSingleItem();
        warning.Code.ShouldBe("InventoryCounts.SoftFreezeActive");
        warning.WarehouseId.ShouldBe(seed.WarehouseId);
    }

    [Fact]
    public async Task InStockAssetDisposal_Should_DecrementBalanceAndBecomeTerminal()
    {
        // Arrange
        M7Seed seed = await SeedAsync(assetTracked: true);
        await CreateAndPostOpeningAsync(seed, 1m);
        await using AsyncServiceScope setupScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await setupContext.Assets.SingleAsync(item => item.MaterialId == seed.MaterialId);
        SubmittedDocument disposal = await CreateSubmittedDisposalAsync(seed, asset.Id);

        // Act
        Result<Guid> result = await PostAsync(disposal.Id, disposal.RowVersion, seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AssetMovementHistory latest = await dbContext.AssetMovementHistories
            .Where(item => item.AssetId == asset.Id)
            .OrderByDescending(item => item.MovedAtUtc).ThenByDescending(item => item.Id)
            .FirstAsync();
        latest.MovementType.ShouldBe(AssetMovementType.Disposed);
        decimal balance = await dbContext.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.MaterialId)
            .Select(item => item.Quantity).SingleAsync();
        balance.ShouldBe(0m);
    }

    [Fact]
    public async Task CustodiedAssetDisposal_Should_CloseCustodyWithoutSecondStockDecrement()
    {
        // Arrange
        M7Seed seed = await SeedAsync(assetTracked: true);
        await CreateAndPostOpeningAsync(seed, 1m);
        await using AsyncServiceScope setupScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await setupContext.Assets.SingleAsync(item => item.MaterialId == seed.MaterialId);
        await CreateAndPostAssetIssueAsync(seed, asset.Id);
        SubmittedDocument disposal = await CreateSubmittedDisposalAsync(seed, asset.Id, -0m);

        // Act
        Result<Guid> result = await PostAsync(disposal.Id, disposal.RowVersion, seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Custody custody = await dbContext.Custodies.SingleAsync(item => item.AssetId == asset.Id);
        custody.Status.ShouldBe(CustodyStatus.Closed);
        custody.DisposalDocumentId.ShouldBe(disposal.Id);
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == disposal.Id)).ShouldBeFalse();
        decimal balance = await dbContext.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.MaterialId)
            .Select(item => item.Quantity).SingleAsync();
        balance.ShouldBe(0m);
    }

    [Fact]
    public async Task ConcurrentDisposals_Should_AllowExactlyOneWinnerForSameAsset()
    {
        // Arrange
        M7Seed seed = await SeedAsync(assetTracked: true);
        await CreateAndPostOpeningAsync(seed, 1m);
        await using AsyncServiceScope setupScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await setupContext.Assets.SingleAsync(item => item.MaterialId == seed.MaterialId);
        SubmittedDocument first = await CreateSubmittedDisposalAsync(seed, asset.Id);
        SubmittedDocument second = await CreateSubmittedDisposalAsync(seed, asset.Id);

        // Act
        Result<Guid>[] results = await Task.WhenAll(
            PostAsync(first.Id, first.RowVersion, seed.UserId),
            PostAsync(second.Id, second.RowVersion, seed.UserId));

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        Result<Guid> loser = results.Single(result => result.IsFailure);
        loser.Error.Code.ShouldBe("Disposals.AssetAlreadyDisposed");
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.AssetMovementHistories.CountAsync(item =>
            item.AssetId == asset.Id && item.MovementType == AssetMovementType.Disposed)).ShouldBe(1);
    }

    [Fact]
    public async Task CreateReversal_Should_ReturnExactConflict_ForPostedDisposal()
    {
        // Arrange
        M7Seed seed = await SeedAsync(assetTracked: true);
        await CreateAndPostOpeningAsync(seed, 1m);
        await using AsyncServiceScope setupScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await setupContext.Assets.SingleAsync(item => item.MaterialId == seed.MaterialId);
        SubmittedDocument disposal = await CreateSubmittedDisposalAsync(seed, asset.Id);
        (await PostAsync(disposal.Id, disposal.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentCreateAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync(
            $"warehouse-documents/{disposal.Id}/reversals", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ApiErrorEnvelope? envelope = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        envelope.ShouldNotBeNull();
        envelope.Success.ShouldBeFalse();
        envelope.Error.Code.ShouldBe("DISPOSALS_REVERSAL_NOT_ALLOWED");
    }

    [Fact]
    public async Task CreateDisposal_Should_CreateBatchThroughHttp()
    {
        // Arrange
        M7Seed seed = await SeedAsync(assetTracked: true);
        await CreateAndPostOpeningAsync(seed, 1m);
        await using AsyncServiceScope setupScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await setupContext.Assets.SingleAsync(item => item.MaterialId == seed.MaterialId);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "inventory-adjustments/disposals",
            new { warehouseId = seed.WarehouseId, assetIds = new[] { asset.Id }, reason = "Damaged" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        ApiEnvelope<ResourceIdDto>? envelope =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<ResourceIdDto>>();
        envelope.ShouldNotBeNull();
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        InventoryAdjustment adjustment = await context.InventoryAdjustments.SingleAsync(
            item => item.Id == envelope.Data.Id);
        adjustment.AdjustmentKind.ShouldBe(AdjustmentKind.Disposal);
        (await context.DocumentLines.CountAsync(item => item.DocumentId == adjustment.Id)).ShouldBe(1);
        (await context.DocumentLineAssetSelections.CountAsync(item => item.DocumentId == adjustment.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task AdjustmentLineMutations_Should_RemainAtomicThroughHttp()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync(
            "inventory-adjustments", new { warehouseId = seed.WarehouseId, reason = "Variance" });
        createResponse.EnsureSuccessStatusCode();
        ApiEnvelope<ResourceIdDto>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ResourceIdDto>>();
        created.ShouldNotBeNull();
        Guid documentId = created.Data.Id;

        // Act
        HttpResponseMessage addResponse = await HttpClient.PostAsJsonAsync(
            $"inventory-adjustments/{documentId}/lines",
            new { materialId = seed.MaterialId, difference = -2m, unitId = seed.UnitId,
                reason = "Shortage", expectedRowVersion = 1 });
        addResponse.EnsureSuccessStatusCode();
        ApiEnvelope<ResourceIdDto>? added =
            await addResponse.Content.ReadFromJsonAsync<ApiEnvelope<ResourceIdDto>>();
        added.ShouldNotBeNull();
        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync(
            $"inventory-adjustments/{documentId}/lines/{added.Data.Id}",
            new { difference = 3m, unitId = seed.UnitId, reason = "Surplus", expectedRowVersion = 2 });
        updateResponse.EnsureSuccessStatusCode();
        HttpResponseMessage removeResponse = await HttpClient.DeleteAsync(
            $"inventory-adjustments/{documentId}/lines/{added.Data.Id}?expectedRowVersion=3");

        // Assert
        removeResponse.EnsureSuccessStatusCode();
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.DocumentLines.AnyAsync(item => item.Id == added.Data.Id)).ShouldBeFalse();
        (await context.AdjustmentLines.AnyAsync(item => item.Id == added.Data.Id)).ShouldBeFalse();
    }

    private async Task<M7Seed> SeedAsync(bool assetTracked = false)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        dbContext.Users.Add(User.Create(userId, $"m7-{suffix}@example.com", "M7", "Tester", "hash"));
        dbContext.Organizations.Add(Organization.Create(organizationId, $"Org {suffix}", $"O{suffix}"));
        dbContext.Sites.Add(Site.Create(siteId, organizationId, $"Site {suffix}", $"S{suffix}", null));
        dbContext.Warehouses.Add(Warehouse.Create(warehouseId, siteId, $"Warehouse {suffix}", $"W{suffix}", "Main", true));
        dbContext.UnitsOfMeasure.Add(UnitOfMeasure.Create(unitId, $"Piece {suffix}", $"P{suffix}", "Count"));
        dbContext.MaterialDomains.Add(MaterialDomain.Create(domainId, $"Domain {suffix}", $"D{suffix}"));
        dbContext.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, $"Category {suffix}", $"C{suffix}"));
        dbContext.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, $"Family {suffix}", $"F{suffix}", unitId));
        dbContext.Materials.Add(Material.Create(materialId, familyId, $"Material {suffix}", null,
            $"M{suffix}", assetTracked ? MaterialKind.Asset : MaterialKind.Consumable,
            assetTracked ? TrackingType.Serial : TrackingType.Quantity, false, assetTracked, null));
        var capability = WarehouseCapability.Create(Guid.NewGuid(), warehouseId, domainId);
        dbContext.WarehouseCapabilities.Add(capability);
        dbContext.WarehouseCapabilityOperations.AddRange(
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Receiving),
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Issue),
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Adjustment),
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Count));
        await dbContext.SaveChangesAsync();
        return new M7Seed(userId, siteId, warehouseId, unitId, materialId, assetTracked);
    }

    private async Task CreateAndPostOpeningAsync(M7Seed seed, decimal quantity)
    {
        SubmittedDocument document = await CreateSubmittedDocumentAsync(
            seed, DocumentType.Opening, quantity, OpeningType.Initial, null);
        (await PostAsync(document.Id, document.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
    }

    private async Task<SubmittedDocument> CreateSubmittedAdjustmentAsync(M7Seed seed, decimal difference)
    {
        return await CreateSubmittedDocumentAsync(seed, DocumentType.Adjustment,
            Math.Abs(difference), null, difference);
    }

    private async Task<SubmittedDocument> CreateSubmittedDocumentAsync(
        M7Seed seed,
        DocumentType type,
        decimal quantity,
        OpeningType? openingType,
        decimal? difference)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(Guid.NewGuid(), seed.WarehouseId, type, $"{type}-{suffix}");
        Result<DocumentLine> line = DocumentLine.Create(Guid.NewGuid(), document.Id, seed.MaterialId,
            seed.AssetTracked ? DocumentLineType.Asset : DocumentLineType.Normal,
            quantity, seed.UnitId, quantity, null, null, null, openingType);
        dbContext.WarehouseDocuments.Add(document);
        dbContext.DocumentLines.Add(line.Value);
        if (difference is decimal adjustmentDifference)
        {
            dbContext.InventoryAdjustments.Add(InventoryAdjustment.Create(
                document.Id, null, AdjustmentKind.Quantity, "Count variance").Value);
            dbContext.AdjustmentLines.Add(AdjustmentLine.Create(
                line.Value.Id, document.Id, adjustmentDifference, "Count variance").Value);
        }

        var attachment = DocumentAttachment.Create(Guid.NewGuid(), document.Id,
            AttachmentType.SignedOriginal, $"m7/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, seed.UserId, DateTime.UtcNow);
        dbContext.DocumentAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();
        return new SubmittedDocument(document.Id, document.RowVersion);
    }

    private async Task<SubmittedDocument> CreateSubmittedDisposalAsync(M7Seed seed, Guid assetId, decimal difference = -1m)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), seed.WarehouseId, DocumentType.Adjustment, $"DIS-{suffix}");
        Result<DocumentLine> line = DocumentLine.Create(Guid.NewGuid(), document.Id, seed.MaterialId,
            DocumentLineType.Asset, 1m, seed.UnitId, 1m, null, null, null);
        dbContext.WarehouseDocuments.Add(document);
        dbContext.DocumentLines.Add(line.Value);
        dbContext.InventoryAdjustments.Add(InventoryAdjustment.Create(
            document.Id, null, AdjustmentKind.Disposal, "Unserviceable asset").Value);
        dbContext.AdjustmentLines.Add(AdjustmentLine.Create(
            line.Value.Id, document.Id, difference, "Unserviceable asset", allowZero: true).Value);
        dbContext.DocumentLineAssetSelections.Add(DocumentLineAssetSelection.Create(
            Guid.NewGuid(), document.Id, line.Value.Id, assetId).Value);
        var attachment = DocumentAttachment.Create(Guid.NewGuid(), document.Id,
            AttachmentType.SignedOriginal, $"m7/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, seed.UserId, DateTime.UtcNow);
        dbContext.DocumentAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();
        return new SubmittedDocument(document.Id, document.RowVersion);
    }

    private async Task CreateAndPostAssetIssueAsync(M7Seed seed, Guid assetId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), seed.WarehouseId, DocumentType.Issue, $"ISS-{suffix}");
        Result<DocumentLine> line = DocumentLine.Create(Guid.NewGuid(), document.Id, seed.MaterialId,
            DocumentLineType.Asset, 1m, seed.UnitId, 1m, null, null, null);
        dbContext.WarehouseDocuments.Add(document);
        dbContext.DocumentLines.Add(line.Value);
        dbContext.IssueTos.Add(IssueTo.Create(
            document.Id, PartyType.Site, seed.SiteId, "Operational issue").Value);
        dbContext.DocumentLineAssetSelections.Add(DocumentLineAssetSelection.Create(
            Guid.NewGuid(), document.Id, line.Value.Id, assetId).Value);
        var attachment = DocumentAttachment.Create(Guid.NewGuid(), document.Id,
            AttachmentType.SignedOriginal, $"m7/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, seed.UserId, DateTime.UtcNow);
        dbContext.DocumentAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();
        (await PostAsync(document.Id, document.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
    }

    private Task StartHardFreezeAsync(M7Seed seed) => StartCountAsync(seed, FreezePolicy.HardFreeze);

    private async Task StartCountAsync(M7Seed seed, FreezePolicy policy)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        InventoryCount count = InventoryCount.Plan(Guid.NewGuid(), seed.WarehouseId, seed.UserId,
            InventoryCountType.Surprise, InventoryCountScopeType.EntireWarehouse,
            null, policy, DateTime.UtcNow).Value;
        count.Start(DateTime.UtcNow.AddTicks(1)).IsSuccess.ShouldBeTrue();
        dbContext.InventoryCounts.Add(count);
        await dbContext.SaveChangesAsync();
    }

    private async Task<Result<Guid>> PostAsync(Guid documentId, int rowVersion, Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        Result<PostingOutcome> result = await coordinator.PostAsync(
            documentId, rowVersion, userId, CancellationToken.None);
        return result.IsFailure
            ? Result.Failure<Guid>(result.Error)
            : result.Value.DocumentId;
    }

    private async Task<Result<PostingOutcome>> PostWithOutcomeAsync(
        Guid documentId,
        int rowVersion,
        Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator =
            scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        return await coordinator.PostAsync(documentId, rowVersion, userId, CancellationToken.None);
    }

    private async Task GrantInventoryCountViewAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"M7 freeze viewer {roleId:N}", null));
        context.RolePermissions.AddRange(
            RolePermission.Create(roleId, WellKnownPermissions.InventoryCountsViewId),
            RolePermission.Create(roleId, WellKnownPermissions.WarehouseDocumentsReviewId));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            userId,
            roleId,
            ScopeType.Warehouse,
            warehouseId));
        await context.SaveChangesAsync();
    }

    private async Task GrantWarehouseDocumentCreateAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"M7 reversal {roleId:N}", null));
        context.RolePermissions.Add(RolePermission.Create(
            roleId, WellKnownPermissions.WarehouseDocumentsCreateId));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private async Task GrantWarehouseDocumentPermissionsAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"M7 adjustment {roleId:N}", null));
        context.RolePermissions.AddRange(
            RolePermission.Create(roleId, WellKnownPermissions.WarehouseDocumentsCreateId),
            RolePermission.Create(roleId, WellKnownPermissions.WarehouseDocumentsEditId));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private sealed record M7Seed(Guid UserId, Guid SiteId, Guid WarehouseId, Guid UnitId, Guid MaterialId, bool AssetTracked);
    private sealed record SubmittedDocument(Guid Id, int RowVersion);
    private sealed record ActiveFreezeDto(Guid CountId, FreezePolicy FreezePolicy);
    private sealed record FreezeStatusDto(
        Guid WarehouseId,
        bool IsPostingBlocked,
        bool HasSoftFreezeWarning,
        IReadOnlyList<ActiveFreezeDto> ActiveCounts);
    private sealed record PostingWarningDto(
        string Code,
        string Message,
        Guid CountId,
        Guid WarehouseId);
    private sealed record PostDocumentDto(
        Guid DocumentId,
        IReadOnlyList<PostingWarningDto> Warnings);
    private sealed record ApiErrorEnvelope(bool Success, ApiError Error);
    private sealed record ApiError(string Code);
    private sealed record ResourceIdDto(Guid Id);
}
