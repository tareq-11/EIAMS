using Application.Abstractions.Posting;
using Domain.Common;
using Domain.Assets;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.Organizations;
using Domain.ReceivingInfos;
using Domain.Sites;
using Domain.StockMovements;
using Domain.UnitsOfMeasure;
using Domain.Users;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel;

namespace IntegrationTests.M4;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M4PostingTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task ReceivingPost_Should_CreateMovementsBalancesAndPerUnitAssets()
    {
        M4Seed seed = await SeedCatalogAsync(includeReceivingCapability: true);
        SubmittedDocument document = await CreateSubmittedDocumentAsync(
            seed,
            DocumentType.Receiving,
            [
                new LineSpec(seed.AssetMaterialId, 2m),
                new LineSpec(seed.NormalMaterialId, 3.5m)
            ]);

        Result<Guid> result = await PostAsync(document.Id, document.RowVersion, seed.UserId);

        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        WarehouseDocument posted = await dbContext.WarehouseDocuments
            .AsNoTracking()
            .SingleAsync(item => item.Id == document.Id);
        posted.DocumentStatus.ShouldBe(DocumentStatus.Posted);

        List<StockMovement> movements = await dbContext.StockMovements
            .AsNoTracking()
            .Where(item => item.DocumentId == document.Id)
            .OrderBy(item => item.MaterialId)
            .ToListAsync();
        movements.Count.ShouldBe(2);
        movements.ShouldAllBe(item => item.MovementType == MovementType.Receipt);
        movements.Sum(item => item.QuantityDelta).ShouldBe(5.5m);

        decimal assetBalance = await dbContext.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.AssetMaterialId)
            .Select(item => item.Quantity)
            .SingleAsync();
        decimal normalBalance = await dbContext.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.NormalMaterialId)
            .Select(item => item.Quantity)
            .SingleAsync();
        assetBalance.ShouldBe(2m);
        normalBalance.ShouldBe(3.5m);

        List<Asset> assets = await dbContext.Assets
            .AsNoTracking()
            .Where(item => item.ReceiptLineId == document.LineIds[0])
            .ToListAsync();
        assets.Count.ShouldBe(2);
        assets.Select(item => item.AssetNumber).Distinct().Count().ShouldBe(2);
        assets.ShouldAllBe(item => item.MaterialId == seed.AssetMaterialId);
        assets.ShouldAllBe(item => item.WarehouseId == seed.WarehouseId);
    }

    [Fact]
    public async Task ReceivingPost_Should_RollBackAllEffects_WhenCapabilityIsMissing()
    {
        M4Seed seed = await SeedCatalogAsync(includeReceivingCapability: false);
        SubmittedDocument document = await CreateSubmittedDocumentAsync(
            seed,
            DocumentType.Receiving,
            [new LineSpec(seed.AssetMaterialId, 2m)]);

        Result<Guid> result = await PostAsync(document.Id, document.RowVersion, seed.UserId);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("WarehouseCapabilities.NotGranted");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        DocumentStatus status = await dbContext.WarehouseDocuments
            .Where(item => item.Id == document.Id)
            .Select(item => item.DocumentStatus)
            .SingleAsync();
        status.ShouldBe(DocumentStatus.Submitted);
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == document.Id)).ShouldBeFalse();
        (await dbContext.InventoryBalances.AnyAsync(item => item.WarehouseId == seed.WarehouseId)).ShouldBeFalse();
        (await dbContext.Assets.AnyAsync(item => item.ReceiptLineId == document.LineIds[0])).ShouldBeFalse();
    }

    [Fact]
    public async Task ConcurrentInitialOpenings_Should_AllowExactlyOnePost()
    {
        M4Seed seed = await SeedCatalogAsync(includeReceivingCapability: false);
        SubmittedDocument first = await CreateSubmittedDocumentAsync(
            seed,
            DocumentType.Opening,
            [new LineSpec(seed.AssetMaterialId, 2m, OpeningType.Initial)]);
        SubmittedDocument second = await CreateSubmittedDocumentAsync(
            seed,
            DocumentType.Opening,
            [new LineSpec(seed.AssetMaterialId, 2m, OpeningType.Initial)]);

        Result<Guid>[] results = await Task.WhenAll(
            PostAsync(first.Id, first.RowVersion, seed.UserId),
            PostAsync(second.Id, second.RowVersion, seed.UserId));

        results.Count(result => result.IsSuccess).ShouldBe(1);
        Result<Guid> failure = results.Single(result => result.IsFailure);
        failure.Error.Code.ShouldBe("OpeningDocuments.AlreadyInitialized");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<StockMovement> movements = await dbContext.StockMovements
            .AsNoTracking()
            .Where(item =>
                item.WarehouseId == seed.WarehouseId &&
                item.MaterialId == seed.AssetMaterialId &&
                item.MovementType == MovementType.Opening &&
                item.QuantityDelta > 0)
            .ToListAsync();
        movements.Count.ShouldBe(1);

        decimal balance = await dbContext.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.AssetMaterialId)
            .Select(item => item.Quantity)
            .SingleAsync();
        balance.ShouldBe(2m);

        int assetCount = await dbContext.Assets.CountAsync(item =>
            item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.AssetMaterialId);
        assetCount.ShouldBe(2);
    }

    [Fact]
    public async Task ReceivingReversal_Should_NegateBalanceAndRemoveSourceAssetsAtomically()
    {
        M4Seed seed = await SeedCatalogAsync(includeReceivingCapability: true);
        SubmittedDocument source = await CreateSubmittedDocumentAsync(
            seed,
            DocumentType.Receiving,
            [new LineSpec(seed.AssetMaterialId, 2m)]);
        Result<Guid> sourcePost = await PostAsync(source.Id, source.RowVersion, seed.UserId);
        sourcePost.IsSuccess.ShouldBeTrue();

        SubmittedDocument reversal = await CreateSubmittedReversalAsync(seed, source.Id);
        Result<Guid> reversalPost = await PostAsync(reversal.Id, reversal.RowVersion, seed.UserId);

        reversalPost.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        WarehouseDocument sourceAfterReversal = await dbContext.WarehouseDocuments
            .AsNoTracking()
            .SingleAsync(item => item.Id == source.Id);
        sourceAfterReversal.DocumentStatus.ShouldBe(DocumentStatus.Reversed);

        decimal balance = await dbContext.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.AssetMaterialId)
            .Select(item => item.Quantity)
            .SingleAsync();
        balance.ShouldBe(0m);

        decimal movementTotal = await dbContext.StockMovements
            .Where(item =>
                item.WarehouseId == seed.WarehouseId &&
                item.MaterialId == seed.AssetMaterialId)
            .SumAsync(item => item.QuantityDelta);
        movementTotal.ShouldBe(0m);
        (await dbContext.Assets.AnyAsync(item => item.ReceiptLineId == source.LineIds[0])).ShouldBeFalse();
    }

    [Fact]
    public async Task PostedStockMovement_Should_RejectUpdateAndDeleteAtDatabaseLevel()
    {
        M4Seed seed = await SeedCatalogAsync(includeReceivingCapability: true);
        SubmittedDocument document = await CreateSubmittedDocumentAsync(
            seed,
            DocumentType.Receiving,
            [new LineSpec(seed.NormalMaterialId, 3m)]);
        Result<Guid> postResult = await PostAsync(document.Id, document.RowVersion, seed.UserId);
        postResult.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid movementId = await dbContext.StockMovements
            .Where(item => item.DocumentId == document.Id)
            .Select(item => item.Id)
            .SingleAsync();

        PostgresException updateException = await Should.ThrowAsync<PostgresException>(
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE public.stock_movements SET quantity_delta = 99 WHERE id = {movementId}"));
        updateException.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
        updateException.MessageText.ShouldContain("append-only");

        PostgresException deleteException = await Should.ThrowAsync<PostgresException>(
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.stock_movements WHERE id = {movementId}"));
        deleteException.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
        deleteException.MessageText.ShouldContain("append-only");

        decimal quantityDelta = await dbContext.StockMovements
            .Where(item => item.Id == movementId)
            .Select(item => item.QuantityDelta)
            .SingleAsync();
        quantityDelta.ShouldBe(3m);
    }

    [Fact]
    public async Task ReceivingPost_Should_RejectMissingReceivingInfo_WithoutWritingLedgerEffects()
    {
        M4Seed seed = await SeedCatalogAsync(includeReceivingCapability: true);
        SubmittedDocument document = await CreateSubmittedDocumentAsync(
            seed,
            DocumentType.Receiving,
            [new LineSpec(seed.NormalMaterialId, 3m)],
            includeReceivingInfo: false);

        Result<Guid> result = await PostAsync(document.Id, document.RowVersion, seed.UserId);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ReceivingInfo.Required");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == document.Id)).ShouldBeFalse();
        (await dbContext.InventoryBalances.AnyAsync(item => item.WarehouseId == seed.WarehouseId)).ShouldBeFalse();
        DocumentStatus status = await dbContext.WarehouseDocuments
            .Where(item => item.Id == document.Id)
            .Select(item => item.DocumentStatus)
            .SingleAsync();
        status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public async Task OpeningPost_Should_RejectCorrectionLines_WithoutWritingLedgerEffects()
    {
        M4Seed seed = await SeedCatalogAsync(includeReceivingCapability: false);
        SubmittedDocument document = await CreateSubmittedDocumentAsync(
            seed,
            DocumentType.Opening,
            [new LineSpec(seed.NormalMaterialId, 2m, OpeningType.Correction)]);

        Result<Guid> result = await PostAsync(document.Id, document.RowVersion, seed.UserId);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("OpeningDocuments.CorrectionRequiresAdjustment");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == document.Id)).ShouldBeFalse();
        (await dbContext.InventoryBalances.AnyAsync(item => item.WarehouseId == seed.WarehouseId)).ShouldBeFalse();
    }

    [Fact]
    public async Task ReceivingReversal_Should_RollBackLedgerChanges_WhenAssetHasDownstreamUsage()
    {
        M4Seed seed = await SeedCatalogAsync(includeReceivingCapability: true);
        SubmittedDocument source = await CreateSubmittedDocumentAsync(
            seed,
            DocumentType.Receiving,
            [new LineSpec(seed.AssetMaterialId, 2m)]);
        (await PostAsync(source.Id, source.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
        await AddDownstreamOutboundMovementAsync(seed);

        SubmittedDocument reversal = await CreateSubmittedReversalAsync(seed, source.Id);

        await using (AsyncServiceScope beforeScope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext beforeDbContext = beforeScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            int sourceMovementCount = await beforeDbContext.StockMovements
                .CountAsync(item => item.DocumentId == source.Id);
            sourceMovementCount.ShouldBe(1);
        }

        Result<Guid> result = await PostAsync(reversal.Id, reversal.RowVersion, seed.UserId);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Assets.ReversalBlocked");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == reversal.Id)).ShouldBeFalse();
        (await dbContext.StockMovements.CountAsync(item => item.DocumentId == source.Id)).ShouldBe(1);
        (await dbContext.Assets.CountAsync(item => item.ReceiptLineId == source.LineIds[0])).ShouldBe(2);
        DocumentStatus sourceStatus = await dbContext.WarehouseDocuments
            .Where(item => item.Id == source.Id)
            .Select(item => item.DocumentStatus)
            .SingleAsync();
        sourceStatus.ShouldBe(DocumentStatus.Posted);
        DocumentStatus reversalStatus = await dbContext.WarehouseDocuments
            .Where(item => item.Id == reversal.Id)
            .Select(item => item.DocumentStatus)
            .SingleAsync();
        reversalStatus.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public async Task ReceivingInfo_Should_RejectNonReceivingDocumentAtDatabaseLevel()
    {
        M4Seed seed = await SeedCatalogAsync(includeReceivingCapability: false);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            seed.WarehouseId,
            DocumentType.Issue,
            $"ISS-{Guid.NewGuid():N}");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WarehouseDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        Result<ReceivingInfo> info = ReceivingInfo.Create(
            document.Id,
            "Supplier",
            null,
            ReceivingType.Supplier);
        info.IsSuccess.ShouldBeTrue();
        dbContext.ReceivingInfos.Add(info.Value);

        DbUpdateException exception = await Should.ThrowAsync<DbUpdateException>(
            dbContext.SaveChangesAsync(CancellationToken.None));
        PostgresException postgresException = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresException.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        postgresException.ConstraintName.ShouldBe("ck_receiving_info_document_type");
    }

    [Fact]
    public async Task AssetNumber_Should_BeUniqueAtDatabaseLevel()
    {
        M4Seed seed = await SeedCatalogAsync(includeReceivingCapability: true);
        SubmittedDocument document = await CreateSubmittedDocumentAsync(
            seed,
            DocumentType.Receiving,
            [new LineSpec(seed.AssetMaterialId, 1m)]);
        (await PostAsync(document.Id, document.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset existing = await dbContext.Assets.SingleAsync(item => item.ReceiptLineId == document.LineIds[0]);
        Result<Asset> duplicate = Asset.CreateReceived(
            Guid.NewGuid(),
            existing.MaterialId,
            existing.WarehouseId!.Value,
            existing.ReceiptLineId!.Value,
            existing.AssetNumber,
            existing.AcquisitionDate);
        duplicate.IsSuccess.ShouldBeTrue();
        dbContext.Assets.Add(duplicate.Value);

        DbUpdateException exception = await Should.ThrowAsync<DbUpdateException>(
            dbContext.SaveChangesAsync(CancellationToken.None));
        PostgresException postgresException = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresException.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        postgresException.ConstraintName.ShouldBe("ix_assets_asset_number");
    }

    private async Task<M4Seed> SeedCatalogAsync(bool includeReceivingCapability)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];

        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var assetMaterialId = Guid.NewGuid();
        var normalMaterialId = Guid.NewGuid();

        dbContext.Users.Add(User.Create(
            userId,
            $"m4-{suffix}@example.com",
            "M4",
            "Tester",
            "not-used-by-posting-tests"));
        dbContext.Organizations.Add(Organization.Create(organizationId, $"Org {suffix}", $"O{suffix}"));
        dbContext.Sites.Add(Site.Create(siteId, organizationId, $"Site {suffix}", $"S{suffix}", null));
        dbContext.Warehouses.Add(Warehouse.Create(
            warehouseId,
            siteId,
            $"Warehouse {suffix}",
            $"W{suffix}",
            "Main",
            canHoldStock: true));
        dbContext.UnitsOfMeasure.Add(UnitOfMeasure.Create(unitId, $"Piece {suffix}", $"P{suffix}", "Count"));
        dbContext.MaterialDomains.Add(MaterialDomain.Create(domainId, $"Domain {suffix}", $"D{suffix}"));
        dbContext.MaterialCategories.Add(MaterialCategory.Create(
            categoryId,
            domainId,
            null,
            $"Category {suffix}",
            $"C{suffix}"));
        dbContext.MaterialFamilies.Add(MaterialFamily.Create(
            familyId,
            categoryId,
            $"Family {suffix}",
            $"F{suffix}",
            unitId));
        dbContext.Materials.Add(Material.Create(
            assetMaterialId,
            familyId,
            $"أصل {suffix}",
            $"Asset {suffix}",
            $"A{suffix}",
            MaterialKind.Asset,
            TrackingType.Serial,
            hasExpiry: false,
            requiresAssetNumber: true,
            attributes: null));
        dbContext.Materials.Add(Material.Create(
            normalMaterialId,
            familyId,
            $"مادة {suffix}",
            $"Material {suffix}",
            $"M{suffix}",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            hasExpiry: false,
            requiresAssetNumber: false,
            attributes: null));

        if (includeReceivingCapability)
        {
            var capabilityId = Guid.NewGuid();
            dbContext.WarehouseCapabilities.Add(WarehouseCapability.Create(
                capabilityId,
                warehouseId,
                domainId));
            dbContext.WarehouseCapabilityOperations.Add(WarehouseCapabilityOperation.Create(
                Guid.NewGuid(),
                capabilityId,
                OperationType.Receiving));
        }

        await dbContext.SaveChangesAsync();

        return new M4Seed(
            userId,
            warehouseId,
            unitId,
            assetMaterialId,
            normalMaterialId);
    }

    private async Task<SubmittedDocument> CreateSubmittedDocumentAsync(
        M4Seed seed,
        DocumentType documentType,
        IReadOnlyList<LineSpec> lineSpecs,
        bool includeReceivingInfo = true)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];

        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            seed.WarehouseId,
            documentType,
            $"M4-{suffix}");
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        dbContext.WarehouseDocuments.Add(document);

        var lineIds = new List<Guid>();

        foreach (LineSpec lineSpec in lineSpecs)
        {
            var lineId = Guid.NewGuid();
            lineIds.Add(lineId);
            Result<DocumentLine> lineResult = DocumentLine.Create(
                lineId,
                document.Id,
                lineSpec.MaterialId,
                lineSpec.MaterialId == seed.AssetMaterialId
                    ? DocumentLineType.Asset
                    : DocumentLineType.Normal,
                lineSpec.Quantity,
                seed.UnitId,
                lineSpec.Quantity,
                unitPrice: null,
                batchNumber: null,
                expiryDate: null,
                lineSpec.OpeningType);
            lineResult.IsSuccess.ShouldBeTrue();
            dbContext.DocumentLines.Add(lineResult.Value);
        }

        if (documentType == DocumentType.Receiving && includeReceivingInfo)
        {
            Result<ReceivingInfo> receivingInfo = ReceivingInfo.Create(
                document.Id,
                $"Supplier {suffix}",
                $"INV-{suffix}",
                ReceivingType.Supplier);
            receivingInfo.IsSuccess.ShouldBeTrue();
            dbContext.ReceivingInfos.Add(receivingInfo.Value);
        }

        await dbContext.SaveChangesAsync();
        await AddSignedOriginalAndSubmitAsync(dbContext, document, seed.UserId, suffix);

        return new SubmittedDocument(document.Id, document.RowVersion, lineIds);
    }

    private async Task AddDownstreamOutboundMovementAsync(M4Seed seed)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            seed.WarehouseId,
            DocumentType.Issue,
            $"ISS-{Guid.NewGuid():N}");
        Result<DocumentLine> lineResult = DocumentLine.Create(
            Guid.NewGuid(),
            document.Id,
            seed.AssetMaterialId,
            DocumentLineType.Asset,
            1m,
            seed.UnitId,
            1m,
            null,
            null,
            null);
        lineResult.IsSuccess.ShouldBeTrue();

        dbContext.WarehouseDocuments.Add(document);
        dbContext.DocumentLines.Add(lineResult.Value);
        await dbContext.SaveChangesAsync();

        string suffix = Guid.NewGuid().ToString("N")[..12];
        document.UpdatePaperReference($"IP-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        await AddSignedOriginalAndSubmitAsync(dbContext, document, seed.UserId, suffix);
        document.MarkPosted(seed.UserId, DateTime.UtcNow).IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();

        Result<StockMovement> movementResult = StockMovement.Create(
            Guid.NewGuid(),
            seed.WarehouseId,
            seed.AssetMaterialId,
            document.Id,
            lineResult.Value.Id,
            MovementType.Issue,
            -1m,
            seed.UserId,
            DateTime.UtcNow);
        movementResult.IsSuccess.ShouldBeTrue();
        dbContext.StockMovements.Add(movementResult.Value);
        await dbContext.SaveChangesAsync();
    }

    private async Task<SubmittedDocument> CreateSubmittedReversalAsync(M4Seed seed, Guid sourceDocumentId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument source = await dbContext.WarehouseDocuments
            .SingleAsync(item => item.Id == sourceDocumentId);
        List<DocumentLine> sourceLines = await dbContext.DocumentLines
            .Where(item => item.DocumentId == sourceDocumentId)
            .ToListAsync();
        string suffix = Guid.NewGuid().ToString("N")[..12];

        var reversal = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            source.WarehouseId,
            source.DocumentType,
            $"REV-{suffix}",
            source.Id);
        reversal.UpdatePaperReference($"RP-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        dbContext.WarehouseDocuments.Add(reversal);

        var reversalLineIds = new List<Guid>();

        foreach (DocumentLine sourceLine in sourceLines)
        {
            var reversalLineId = Guid.NewGuid();
            reversalLineIds.Add(reversalLineId);
            Result<DocumentLine> reversalLine = DocumentLine.Create(
                reversalLineId,
                reversal.Id,
                sourceLine.MaterialId,
                sourceLine.LineType,
                sourceLine.Quantity,
                sourceLine.UnitId,
                sourceLine.BaseQuantity,
                sourceLine.UnitPrice,
                sourceLine.BatchNumber,
                sourceLine.ExpiryDate,
                sourceLine.OpeningType,
                sourceLine.Id);
            reversalLine.IsSuccess.ShouldBeTrue();
            dbContext.DocumentLines.Add(reversalLine.Value);
        }

        await dbContext.SaveChangesAsync();
        await AddSignedOriginalAndSubmitAsync(dbContext, reversal, seed.UserId, suffix);

        return new SubmittedDocument(reversal.Id, reversal.RowVersion, reversalLineIds);
    }

    private static async Task AddSignedOriginalAndSubmitAsync(
        ApplicationDbContext dbContext,
        WarehouseDocument document,
        Guid userId,
        string suffix)
    {
        var attachmentId = Guid.NewGuid();
        dbContext.DocumentAttachments.Add(DocumentAttachment.Create(
            attachmentId,
            document.Id,
            AttachmentType.SignedOriginal,
            $"m4-tests/{suffix}.pdf",
            $"{suffix}.pdf",
            "application/pdf",
            100,
            suffix,
            userId,
            DateTime.UtcNow));
        await dbContext.SaveChangesAsync();

        document.SetSignedCopy(attachmentId).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();
    }

    private async Task<Result<Guid>> PostAsync(
        Guid documentId,
        int rowVersion,
        Guid postedBy)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator =
            scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();

        Result<PostingOutcome> result = await coordinator.PostAsync(
            documentId, rowVersion, postedBy, CancellationToken.None);
        return result.IsFailure
            ? Result.Failure<Guid>(result.Error)
            : result.Value.DocumentId;
    }

    private sealed record M4Seed(
        Guid UserId,
        Guid WarehouseId,
        Guid UnitId,
        Guid AssetMaterialId,
        Guid NormalMaterialId);

    private sealed record LineSpec(
        Guid MaterialId,
        decimal Quantity,
        OpeningType? OpeningType = null);

    private sealed record SubmittedDocument(
        Guid Id,
        int RowVersion,
        IReadOnlyList<Guid> LineIds);
}
