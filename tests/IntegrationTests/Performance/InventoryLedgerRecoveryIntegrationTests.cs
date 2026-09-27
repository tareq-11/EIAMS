using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.InventoryBalances;
using Domain.Materials;
using Domain.ReceivingInfos;
using Domain.StockMovements;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using IntegrationTests.Regression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class InventoryLedgerRecoveryIntegrationTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public InventoryLedgerRecoveryIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task RebuildAll_ShouldRestoreDriftedAndMissingBalancesOnlyFromImmutableLedger()
    {
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        WarehouseDocument firstReceipt = await CreateDocumentAsync(seed, seed.WarehouseId, DocumentType.Receiving,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 10m)]);
        Result<PostingOutcome> firstPost = await PostAsync(firstReceipt, seed.ManagerUserId);
        firstPost.IsSuccess.ShouldBeTrue(firstPost.IsFailure ? firstPost.Error.ToString() : null);
        WarehouseDocument secondReceipt = await CreateDocumentAsync(seed, seed.DestinationWarehouseId,
            DocumentType.Receiving, [(seed.NormalMaterialId, DocumentLineType.Normal, 4m)]);
        Result<PostingOutcome> secondPost = await PostAsync(secondReceipt, seed.ManagerUserId);
        secondPost.IsSuccess.ShouldBeTrue(secondPost.IsFailure ? secondPost.Error.ToString() : null);

        Guid[] ledgerMovementIdsBefore;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ledgerMovementIdsBefore = await context.StockMovements.AsNoTracking()
                .Select(movement => movement.Id)
                .OrderBy(id => id)
                .ToArrayAsync();

            InventoryBalance destinationBalance = await context.InventoryBalances.SingleAsync(balance =>
                balance.WarehouseId == seed.DestinationWarehouseId && balance.MaterialId == seed.NormalMaterialId);
            destinationBalance.SetQuantity(900m, DateTime.UtcNow).IsSuccess.ShouldBeTrue();

            var ledgerlessBalance = InventoryBalance.CreateZero(
                Guid.NewGuid(), seed.WarehouseId, seed.AssetMaterialId, DateTime.UtcNow);
            ledgerlessBalance.SetQuantity(12m, DateTime.UtcNow).IsSuccess.ShouldBeTrue();
            context.InventoryBalances.Add(ledgerlessBalance);
            await context.SaveChangesAsync();

            int deleted = await context.InventoryBalances
                .Where(balance => balance.WarehouseId == seed.WarehouseId &&
                    balance.MaterialId == seed.NormalMaterialId)
                .ExecuteDeleteAsync();
            deleted.ShouldBe(1);
        }

        Result<int> rebuild;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IInventoryBalanceRebuilder rebuilder = scope.ServiceProvider
                .GetRequiredService<IInventoryBalanceRebuilder>();
            rebuild = await rebuilder.RebuildAllAsync(seed.ManagerUserId, DateTime.UtcNow, CancellationToken.None);
        }

        rebuild.IsSuccess.ShouldBeTrue(rebuild.IsFailure ? rebuild.Error.ToString() : null);
        rebuild.Value.ShouldBeGreaterThanOrEqualTo(3);

        await using AsyncServiceScope verificationScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var balances = await verificationContext.InventoryBalances.AsNoTracking()
            .Where(balance => balance.WarehouseId == seed.WarehouseId ||
                              balance.WarehouseId == seed.DestinationWarehouseId)
            .Select(balance => new { balance.WarehouseId, balance.MaterialId, balance.Quantity })
            .ToListAsync();
        balances.Count.ShouldBe(3);
        balances.Single(balance => balance.WarehouseId == seed.WarehouseId &&
            balance.MaterialId == seed.NormalMaterialId).Quantity.ShouldBe(10m);
        balances.Single(balance => balance.WarehouseId == seed.DestinationWarehouseId &&
            balance.MaterialId == seed.NormalMaterialId).Quantity.ShouldBe(4m);
        balances.Single(balance => balance.WarehouseId == seed.WarehouseId &&
            balance.MaterialId == seed.AssetMaterialId).Quantity.ShouldBe(0m);

        var ledgerTotals = await verificationContext.StockMovements.AsNoTracking()
            .GroupBy(movement => new { movement.WarehouseId, movement.MaterialId })
            .Select(group => new { group.Key.WarehouseId, group.Key.MaterialId, Quantity = group.Sum(m => m.QuantityDelta) })
            .ToListAsync();
        foreach (var balance in balances)
        {
            decimal ledgerQuantity = ledgerTotals
                .Where(total => total.WarehouseId == balance.WarehouseId && total.MaterialId == balance.MaterialId)
                .Select(total => total.Quantity)
                .SingleOrDefault();
            balance.Quantity.ShouldBe(ledgerQuantity);
        }

        Guid[] ledgerMovementIdsAfter = await verificationContext.StockMovements.AsNoTracking()
            .Select(movement => movement.Id)
            .OrderBy(id => id)
            .ToArrayAsync();
        ledgerMovementIdsAfter.ShouldBe(ledgerMovementIdsBefore);
    }

    [Fact]
    public async Task ConcurrentAdjustmentAndReversalPosting_ShouldKeepSignedLedgerAndBalanceInParity()
    {
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        WarehouseDocument opening = await CreateDocumentAsync(seed, seed.WarehouseId, DocumentType.Opening,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 100m)]);
        Result<PostingOutcome> openingPost = await PostAsync(opening, seed.ManagerUserId);
        openingPost.IsSuccess.ShouldBeTrue(openingPost.IsFailure ? openingPost.Error.ToString() : null);

        WarehouseDocument sourceAdjustment = await CreateAdjustmentAsync(seed, 10m);
        Result<PostingOutcome> sourcePost = await PostAsync(sourceAdjustment, seed.ManagerUserId);
        sourcePost.IsSuccess.ShouldBeTrue(sourcePost.IsFailure ? sourcePost.Error.ToString() : null);
        WarehouseDocument reversal = await CreateSubmittedReversalAsync(seed, sourceAdjustment.Id);
        WarehouseDocument concurrentAdjustment = await CreateAdjustmentAsync(seed, -3m);

        Result<PostingOutcome>[] results = await Task.WhenAll(
            PostAsync(reversal, seed.ManagerUserId),
            PostAsync(concurrentAdjustment, seed.ManagerUserId));

        foreach (Result<PostingOutcome> result in results)
        {
            result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.ToString() : null);
        }
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        decimal ledgerQuantity = await context.StockMovements.AsNoTracking()
            .Where(movement => movement.WarehouseId == seed.WarehouseId &&
                movement.MaterialId == seed.NormalMaterialId)
            .SumAsync(movement => movement.QuantityDelta);
        decimal balanceQuantity = await context.InventoryBalances.AsNoTracking()
            .Where(balance => balance.WarehouseId == seed.WarehouseId &&
                balance.MaterialId == seed.NormalMaterialId)
            .Select(balance => balance.Quantity)
            .SingleAsync();
        ledgerQuantity.ShouldBe(97m);
        balanceQuantity.ShouldBe(ledgerQuantity);
        (await context.StockMovements.CountAsync(movement => movement.WarehouseId == seed.WarehouseId &&
            movement.MaterialId == seed.NormalMaterialId)).ShouldBe(4);
        (await context.StockMovements.Where(movement => movement.DocumentId == sourceAdjustment.Id)
            .Select(movement => movement.QuantityDelta).SingleAsync()).ShouldBe(10m);
        (await context.StockMovements.Where(movement => movement.DocumentId == reversal.Id)
            .Select(movement => movement.QuantityDelta).SingleAsync()).ShouldBe(-10m);
        (await context.StockMovements.Where(movement => movement.DocumentId == concurrentAdjustment.Id)
            .Select(movement => movement.QuantityDelta).SingleAsync()).ShouldBe(-3m);
        (await context.InventoryAdjustments.SingleAsync(adjustment => adjustment.Id == sourceAdjustment.Id))
            .Status.ShouldBe(InventoryAdjustmentStatus.Reversed);
        (await context.InventoryAdjustments.SingleAsync(adjustment => adjustment.Id == concurrentAdjustment.Id))
            .Status.ShouldBe(InventoryAdjustmentStatus.Posted);
        (await context.WarehouseDocuments.SingleAsync(document => document.Id == reversal.Id))
            .DocumentStatus.ShouldBe(DocumentStatus.Posted);
    }

    private async Task<WarehouseDocument> CreateDocumentAsync(
        RegressionSeedData seed,
        Guid warehouseId,
        DocumentType type,
        IReadOnlyList<(Guid MaterialId, DocumentLineType LineType, decimal Quantity)> lines) =>
        await CreateSubmittedDocumentAsync(seed, warehouseId, type, lines);

    private async Task<WarehouseDocument> CreateAdjustmentAsync(RegressionSeedData seed, decimal difference) =>
        await CreateSubmittedDocumentAsync(seed, seed.WarehouseId, DocumentType.Adjustment,
            [(seed.NormalMaterialId, DocumentLineType.Normal, Math.Abs(difference))],
            (document, context) =>
            {
                InventoryAdjustment adjustment = InventoryAdjustment.Create(
                    document.Id, null, AdjustmentKind.Quantity, "Concurrent adjustment recovery test").Value;
                DocumentLine line = context.DocumentLines.Local.Single(item => item.DocumentId == document.Id);
                context.InventoryAdjustments.Add(adjustment);
                context.AdjustmentLines.Add(AdjustmentLine.Create(
                    line.Id, document.Id, difference, "Concurrent adjustment recovery test").Value);
            });

    private async Task<WarehouseDocument> CreateSubmittedDocumentAsync(
        RegressionSeedData seed,
        Guid warehouseId,
        DocumentType type,
        IReadOnlyList<(Guid MaterialId, DocumentLineType LineType, decimal Quantity)> lines,
        Action<WarehouseDocument, ApplicationDbContext>? configure = null)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(Guid.NewGuid(), warehouseId, type, $"LEDGER-{suffix}");
        context.WarehouseDocuments.Add(document);
        foreach ((Guid materialId, DocumentLineType lineType, decimal quantity) in lines)
        {
            Material material = await context.Materials.SingleAsync(item => item.Id == materialId);
            DocumentLineProvenance provenance = DocumentLineProvenance.Create(
                material.CatalogVersion, material.MaterialKind, material.TrackingType, material.BaseUnitId).Value;
            context.DocumentLines.Add(DocumentLine.Create(
                Guid.NewGuid(), document.Id, materialId, lineType, quantity, null, quantity,
                null, null, null, type == DocumentType.Opening ? OpeningType.Initial : null,
                provenance: provenance).Value);
        }

        configure?.Invoke(document, context);
        if (type == DocumentType.Receiving)
        {
            context.ReceivingInfos.Add(ReceivingInfo.Create(
                document.Id, "Ledger recovery supplier", null, ReceivingType.Supplier).Value);
        }

        await context.SaveChangesAsync();
        var attachment = DocumentAttachment.Create(
            Guid.NewGuid(), document.Id, AttachmentType.SignedOriginal, $"ledger/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 16, suffix, seed.KeeperUserId, DateTime.UtcNow);
        context.DocumentAttachments.Add(attachment);
        await context.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"LEDGER-PAPER-{suffix}", DateTime.UtcNow.Year).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        return document;
    }

    private async Task<WarehouseDocument> CreateSubmittedReversalAsync(
        RegressionSeedData seed,
        Guid sourceDocumentId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument source = await context.WarehouseDocuments.SingleAsync(item => item.Id == sourceDocumentId);
        List<DocumentLine> sourceLines = await context.DocumentLines
            .Where(line => line.DocumentId == sourceDocumentId)
            .OrderBy(line => line.CreatedAtUtc)
            .ThenBy(line => line.Id)
            .ToListAsync();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var reversal = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), source.WarehouseId, source.DocumentType, $"REV-{suffix}", source.Id);
        context.WarehouseDocuments.Add(reversal);
        foreach (DocumentLine sourceLine in sourceLines)
        {
            DocumentLine reversalLine = DocumentLine.Create(
                Guid.NewGuid(), reversal.Id, sourceLine.MaterialId, sourceLine.LineType, sourceLine.Quantity,
                sourceLine.UnitId, sourceLine.BaseQuantity, sourceLine.UnitPrice, sourceLine.BatchNumber,
                sourceLine.ExpiryDate, sourceLine.OpeningType, sourceLine.Id, sourceLine.Provenance).Value;
            context.DocumentLines.Add(reversalLine);
        }

        await context.SaveChangesAsync();
        var signedCopy = DocumentAttachment.Create(
            Guid.NewGuid(), reversal.Id, AttachmentType.SignedOriginal, $"rebuild/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 16, suffix, seed.KeeperUserId, DateTime.UtcNow);
        context.DocumentAttachments.Add(signedCopy);
        await context.SaveChangesAsync();
        reversal.SetSignedCopy(signedCopy.Id).IsSuccess.ShouldBeTrue();
        reversal.UpdatePaperReference($"REV-PAPER-{suffix}", DateTime.UtcNow.Year).IsSuccess.ShouldBeTrue();
        reversal.Submit().IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        return reversal;
    }

    private async Task<Result<PostingOutcome>> PostAsync(WarehouseDocument document, Guid postedBy)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = scope.ServiceProvider
            .GetRequiredService<IDocumentPostingCoordinator>();
        return await coordinator.PostAsync(document.Id, document.RowVersion, postedBy, CancellationToken.None);
    }
}
