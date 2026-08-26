using Application.Abstractions.Posting;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.ReceivingInfos;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Regression;

[Collection(nameof(IntegrationTestCollection))]
public sealed class DisposalRegressionTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public DisposalRegressionTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task DisposalFlow_Should_MarkAssetDisposedAndPreventReversal()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // Receive 1 Asset
        WarehouseDocument receiving = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [(seed.AssetMaterialId, DocumentLineType.Asset, 1m)],
            (document, context) =>
            {
                ReceivingInfo info = ReceivingInfo.Create(document.Id, "Vendor", "INV-DISP", ReceivingType.Supplier).Value;
                context.ReceivingInfos.Add(info);
            });

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            (await coordinator.PostAsync(receiving.Id, receiving.RowVersion, seed.ManagerUserId, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        }

        Guid assetId;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Asset asset = await context.Assets.SingleAsync(a => a.MaterialId == seed.AssetMaterialId);
            assetId = asset.Id;
        }

        // 2. Create Disposal Document (Adjustment with kind = Disposal)
        WarehouseDocument disposalDoc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Adjustment,
            seed.KeeperUserId,
            [(seed.AssetMaterialId, DocumentLineType.Asset, 1m)],
            (document, context) =>
            {
                InventoryAdjustment adj = InventoryAdjustment.Create(document.Id, null, AdjustmentKind.Disposal, "Damaged beyond repair").Value;
                context.InventoryAdjustments.Add(adj);

                DocumentLine docLine = context.DocumentLines.Local.Single(l => l.DocumentId == document.Id);
                AdjustmentLine adjLine = AdjustmentLine.Create(docLine.Id, document.Id, -1m, "Damaged beyond repair").Value;
                context.AdjustmentLines.Add(adjLine);

                DocumentLineAssetSelection selection = DocumentLineAssetSelection.Create(Guid.NewGuid(), document.Id, docLine.Id, assetId).Value;
                context.DocumentLineAssetSelections.Add(selection);
            });

        // 3. Post Disposal
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> postResult = await coordinator.PostAsync(disposalDoc.Id, disposalDoc.RowVersion, seed.ManagerUserId, CancellationToken.None);
            postResult.IsSuccess.ShouldBeTrue();

            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // AssetMovementHistory should have Disposed
            AssetMovementHistory history = await context.AssetMovementHistories
                .SingleAsync(h => h.AssetId == assetId && h.DocumentId == disposalDoc.Id);
            history.MovementType.ShouldBe(AssetMovementType.Disposed);

            // Reversal on Disposal is forbidden
            InventoryAdjustment adj = await context.InventoryAdjustments.SingleAsync(a => a.Id == disposalDoc.Id);
            Result reverseResult = adj.MarkReversed();
            reverseResult.IsFailure.ShouldBeTrue();
        }
    }
}
