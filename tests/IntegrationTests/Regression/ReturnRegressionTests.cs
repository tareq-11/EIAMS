using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentLines;
using Domain.IssueTos;
using Domain.ReturnInfos;
using Domain.StockMovements;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Regression;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ReturnRegressionTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public ReturnRegressionTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ReturnFlow_Consumable_Should_CreditBalanceAndCreateReceiptMovement()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // Receive 10 units
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

        // Issue 4 units
        WarehouseDocument issueDoc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Issue,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 4m)],
            (document, context) =>
            {
                IssueTo issueTo = IssueTo.Create(document.Id, PartyType.OrganizationalUnit, seed.OrgUnitId, "Deploy project").Value;
                context.IssueTos.Add(issueTo);
            });

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            (await coordinator.PostAsync(issueDoc.Id, issueDoc.RowVersion, seed.ManagerUserId, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        }

        // 2. Return 2 units referencing original issue document
        WarehouseDocument returnDoc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Return,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 2m)],
            (document, context) =>
            {
                ReturnInfo returnInfo = ReturnInfo.Create(document.Id, issueDoc.Id, "Unused surplus returned").Value;
                context.ReturnInfos.Add(returnInfo);
            });

        // 3. Post Return Document
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> postResult = await coordinator.PostAsync(returnDoc.Id, returnDoc.RowVersion, seed.ManagerUserId, CancellationToken.None);
            postResult.IsSuccess.ShouldBeTrue();

            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            WarehouseDocument posted = await context.WarehouseDocuments.SingleAsync(d => d.Id == returnDoc.Id);
            posted.DocumentStatus.ShouldBe(DocumentStatus.Posted);

            // Movement should be Receipt
            StockMovement movement = await context.StockMovements.SingleAsync(m => m.DocumentId == returnDoc.Id);
            movement.MovementType.ShouldBe(MovementType.Receipt);
            movement.QuantityDelta.ShouldBe(2m);

            // Remaining balance should be: 10 - 4 + 2 = 8
            decimal balance = await context.InventoryBalances
                .Where(b => b.WarehouseId == seed.WarehouseId && b.MaterialId == seed.NormalMaterialId)
                .Select(b => b.Quantity)
                .SingleAsync();
            balance.ShouldBe(8m);
        }
    }
}
