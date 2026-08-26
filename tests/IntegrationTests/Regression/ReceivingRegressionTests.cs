using Application.Abstractions.Posting;
using Domain.Assets;
using Domain.Common;
using Domain.DocumentLines;
using Domain.ReceivingInfos;
using Domain.StockMovements;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Regression;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ReceivingRegressionTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public ReceivingRegressionTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ReceivingFlow_HappyPath_Should_CreateMovementsUpdateBalanceAndGenerateAssets()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // 2. Create and Submit Receiving Document
        WarehouseDocument doc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [
                (seed.NormalMaterialId, DocumentLineType.Normal, 10m),
                (seed.AssetMaterialId, DocumentLineType.Asset, 2m)
            ],
            (document, context) =>
            {
                ReceivingInfo info = ReceivingInfo.Create(document.Id, "Supplier ACME", "INV-999", ReceivingType.Supplier).Value;
                context.ReceivingInfos.Add(info);
            });

        // 3. Post Document
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator postingCoordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        Result<PostingOutcome> postResult = await postingCoordinator.PostAsync(doc.Id, doc.RowVersion, seed.ManagerUserId, CancellationToken.None);

        // 4. Assert
        postResult.IsSuccess.ShouldBeTrue($"Posting failed: {postResult.Error.Code} - {postResult.Error.Description}");

        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument postedDoc = await context.WarehouseDocuments.SingleAsync(d => d.Id == doc.Id);
        postedDoc.DocumentStatus.ShouldBe(DocumentStatus.Posted);

        // Verify Movements
        List<StockMovement> movements = await context.StockMovements.Where(m => m.DocumentId == doc.Id).ToListAsync();
        movements.Count.ShouldBe(2);
        movements.ShouldAllBe(m => m.MovementType == MovementType.Receipt);
        movements.Single(m => m.MaterialId == seed.NormalMaterialId).QuantityDelta.ShouldBe(10m);
        movements.Single(m => m.MaterialId == seed.AssetMaterialId).QuantityDelta.ShouldBe(2m);

        // Verify Balances
        decimal normalBalance = await context.InventoryBalances
            .Where(b => b.WarehouseId == seed.WarehouseId && b.MaterialId == seed.NormalMaterialId)
            .Select(b => b.Quantity)
            .SingleAsync();
        normalBalance.ShouldBe(10m);

        decimal assetBalance = await context.InventoryBalances
            .Where(b => b.WarehouseId == seed.WarehouseId && b.MaterialId == seed.AssetMaterialId)
            .Select(b => b.Quantity)
            .SingleAsync();
        assetBalance.ShouldBe(2m);

        // Verify Assets created for asset material line
        List<Asset> assets = await context.Assets.Where(a => a.MaterialId == seed.AssetMaterialId && a.WarehouseId == seed.WarehouseId).ToListAsync();
        assets.Count.ShouldBe(2);
        assets.Select(a => a.AssetNumber).Distinct().Count().ShouldBe(2);
    }
}
