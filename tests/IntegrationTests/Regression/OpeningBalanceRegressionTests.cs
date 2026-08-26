using Application.Abstractions.Posting;
using Domain.Assets;
using Domain.Common;
using Domain.DocumentLines;
using Domain.StockMovements;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Regression;

[Collection(nameof(IntegrationTestCollection))]
public sealed class OpeningBalanceRegressionTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public OpeningBalanceRegressionTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task OpeningBalanceFlow_Should_EstablishInitialBalanceAndCreateOpeningMovements()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // 2. Create Opening Balance document with both consumable and asset lines
        WarehouseDocument doc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Opening,
            seed.KeeperUserId,
            [
                (seed.NormalMaterialId, DocumentLineType.Normal, 50m),
                (seed.AssetMaterialId, DocumentLineType.Asset, 3m)
            ]);

        // 3. Post Opening Balance
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        Result<PostingOutcome> postResult = await coordinator.PostAsync(doc.Id, doc.RowVersion, seed.ManagerUserId, CancellationToken.None);

        postResult.IsSuccess.ShouldBeTrue();

        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument posted = await context.WarehouseDocuments.SingleAsync(d => d.Id == doc.Id);
        posted.DocumentStatus.ShouldBe(DocumentStatus.Posted);

        // Verify Movements
        List<StockMovement> movements = await context.StockMovements.Where(m => m.DocumentId == doc.Id).ToListAsync();
        movements.Count.ShouldBe(2);
        movements.ShouldAllBe(m => m.MovementType == MovementType.Opening);

        // Verify established balances
        decimal normalBalance = await context.InventoryBalances
            .Where(b => b.WarehouseId == seed.WarehouseId && b.MaterialId == seed.NormalMaterialId)
            .Select(b => b.Quantity)
            .SingleAsync();
        normalBalance.ShouldBe(50m);

        decimal assetBalance = await context.InventoryBalances
            .Where(b => b.WarehouseId == seed.WarehouseId && b.MaterialId == seed.AssetMaterialId)
            .Select(b => b.Quantity)
            .SingleAsync();
        assetBalance.ShouldBe(3m);

        // Verify Assets created
        List<Asset> assets = await context.Assets.Where(a => a.MaterialId == seed.AssetMaterialId).ToListAsync();
        assets.Count.ShouldBe(3);
    }
}
