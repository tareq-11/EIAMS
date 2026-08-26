using Application.Abstractions.Posting;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.IssueTos;
using Domain.StockMovements;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Regression;

[Collection(nameof(IntegrationTestCollection))]
public sealed class IssueRegressionTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public IssueRegressionTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task IssueFlow_NonAsset_Should_DeductBalanceAndCreateNegativeMovement()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // Receive stock first (10 units)
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

        // 2. Create and Submit Issue Document (4 units)
        WarehouseDocument issueDoc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Issue,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 4m)],
            (document, context) =>
            {
                IssueTo issueTo = IssueTo.Create(document.Id, PartyType.OrganizationalUnit, seed.OrgUnitId, "Project deployment").Value;
                context.IssueTos.Add(issueTo);
            });

        // 3. Post Issue Document
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> postResult = await coordinator.PostAsync(issueDoc.Id, issueDoc.RowVersion, seed.ManagerUserId, CancellationToken.None);
            postResult.IsSuccess.ShouldBeTrue();

            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            WarehouseDocument posted = await context.WarehouseDocuments.SingleAsync(d => d.Id == issueDoc.Id);
            posted.DocumentStatus.ShouldBe(DocumentStatus.Posted);

            // Movement should be negative
            StockMovement movement = await context.StockMovements.SingleAsync(m => m.DocumentId == issueDoc.Id);
            movement.MovementType.ShouldBe(MovementType.Issue);
            movement.QuantityDelta.ShouldBe(-4m);

            // Remaining balance should be 6
            decimal remainingBalance = await context.InventoryBalances
                .Where(b => b.WarehouseId == seed.WarehouseId && b.MaterialId == seed.NormalMaterialId)
                .Select(b => b.Quantity)
                .SingleAsync();
            remainingBalance.ShouldBe(6m);
        }
    }

    [Fact]
    public async Task IssueFlow_Should_Fail_When_BalanceIsInsufficient()
    {
        // 1. Arrange & Seed (Warehouse has 0 stock)
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // 2. Create Issue for 5 units without receiving stock
        WarehouseDocument issueDoc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Issue,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 5m)],
            (document, context) =>
            {
                IssueTo issueTo = IssueTo.Create(document.Id, PartyType.OrganizationalUnit, seed.OrgUnitId, "Urgent issue").Value;
                context.IssueTos.Add(issueTo);
            });

        // 3. Post should fail with insufficient stock
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        Result<PostingOutcome> postResult = await coordinator.PostAsync(issueDoc.Id, issueDoc.RowVersion, seed.ManagerUserId, CancellationToken.None);

        postResult.IsFailure.ShouldBeTrue();
    }
}
