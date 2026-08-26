using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentLines;
using Domain.StockMovements;
using Domain.TransferInfos;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Regression;

[Collection(nameof(IntegrationTestCollection))]
public sealed class TransferRegressionTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public TransferRegressionTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AtomicTransferFlow_Should_DeductSource_CreditDestination_AndCreateTwoMovements()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // Receive 20 units into source warehouse
        WarehouseDocument receiving = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 20m)]);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            (await coordinator.PostAsync(receiving.Id, receiving.RowVersion, seed.ManagerUserId, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        }

        // 2. Create and Submit Transfer Document (move 8 units from wh1 to wh2)
        WarehouseDocument transferDoc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Transfer,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 8m)],
            (document, context) =>
            {
                TransferInfo transferInfo = TransferInfo.Create(document.Id, seed.DestinationWarehouseId, "Replenish branch").Value;
                context.TransferInfos.Add(transferInfo);
            });

        // 3. Post Transfer Document
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> postResult = await coordinator.PostAsync(transferDoc.Id, transferDoc.RowVersion, seed.ManagerUserId, CancellationToken.None);
            postResult.IsSuccess.ShouldBeTrue();

            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            WarehouseDocument posted = await context.WarehouseDocuments.SingleAsync(d => d.Id == transferDoc.Id);
            posted.DocumentStatus.ShouldBe(DocumentStatus.Posted);

            // Assert 2 movements created in a single transaction
            List<StockMovement> movements = await context.StockMovements
                .Where(m => m.DocumentId == transferDoc.Id)
                .OrderBy(m => m.WarehouseId)
                .ToListAsync();

            movements.Count.ShouldBe(2);
            StockMovement outMovement = movements.Single(m => m.MovementType == MovementType.TransferOut);
            outMovement.WarehouseId.ShouldBe(seed.WarehouseId);
            outMovement.QuantityDelta.ShouldBe(-8m);

            StockMovement inMovement = movements.Single(m => m.MovementType == MovementType.TransferIn);
            inMovement.WarehouseId.ShouldBe(seed.DestinationWarehouseId);
            inMovement.QuantityDelta.ShouldBe(8m);

            // Assert Balances
            decimal sourceBalance = await context.InventoryBalances
                .Where(b => b.WarehouseId == seed.WarehouseId && b.MaterialId == seed.NormalMaterialId)
                .Select(b => b.Quantity)
                .SingleAsync();
            sourceBalance.ShouldBe(12m); // 20 - 8

            decimal destinationBalance = await context.InventoryBalances
                .Where(b => b.WarehouseId == seed.DestinationWarehouseId && b.MaterialId == seed.NormalMaterialId)
                .Select(b => b.Quantity)
                .SingleAsync();
            destinationBalance.ShouldBe(8m); // 0 + 8
        }
    }
}
