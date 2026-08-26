using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.StockMovements;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Regression;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AdjustmentRegressionTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public AdjustmentRegressionTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AdjustmentFlow_Should_CreateAdjustmentMovementsAndCorrectBalance()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // Receive 10 units first
        WarehouseDocument receiving = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 10m)]);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            (await coordinator.PostAsync(receiving.Id, receiving.RowVersion, seed.ManagerUserId, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        }

        // 2. Create Adjustment Document (e.g. increase by 3 units)
        WarehouseDocument adjDoc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Adjustment,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 3m)],
            (document, context) =>
            {
                InventoryAdjustment adj = InventoryAdjustment.Create(document.Id, null, AdjustmentKind.Quantity, "Found extra stock").Value;
                context.InventoryAdjustments.Add(adj);

                // Line ID must match DocumentLine ID
                DocumentLine docLine = context.DocumentLines.Local.Single(l => l.DocumentId == document.Id);
                AdjustmentLine adjLine = AdjustmentLine.Create(docLine.Id, document.Id, 3m, "Found extra stock").Value;
                context.AdjustmentLines.Add(adjLine);
            });

        // 3. Post Adjustment
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> postResult = await coordinator.PostAsync(adjDoc.Id, adjDoc.RowVersion, seed.ManagerUserId, CancellationToken.None);
            postResult.IsSuccess.ShouldBeTrue();

            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            WarehouseDocument posted = await context.WarehouseDocuments.SingleAsync(d => d.Id == adjDoc.Id);
            posted.DocumentStatus.ShouldBe(DocumentStatus.Posted);

            // Movement should be AdjustmentIn
            StockMovement movement = await context.StockMovements.SingleAsync(m => m.DocumentId == adjDoc.Id);
            movement.MovementType.ShouldBe(MovementType.AdjustmentIn);
            movement.QuantityDelta.ShouldBe(3m);

            // Total balance should now be 13 (10 + 3)
            decimal balance = await context.InventoryBalances
                .Where(b => b.WarehouseId == seed.WarehouseId && b.MaterialId == seed.NormalMaterialId)
                .Select(b => b.Quantity)
                .SingleAsync();
            balance.ShouldBe(13m);
        }
    }
}
