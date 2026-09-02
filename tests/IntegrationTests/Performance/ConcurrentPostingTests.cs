using System.Collections.Concurrent;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.IssueTos;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using IntegrationTests.Regression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ConcurrentPostingTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public ConcurrentPostingTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ConcurrentReceivingPosts_Should_NotDeadlock_AndMaintainCorrectBalance()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        const int concurrentPosts = 5;
        const decimal quantityPerPost = 10m;

        var documents = new List<WarehouseDocument>();
        for (int i = 0; i < concurrentPosts; i++)
        {
            WarehouseDocument doc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
                factory.Services,
                seed.WarehouseId,
                DocumentType.Receiving,
                seed.KeeperUserId,
                [(seed.NormalMaterialId, DocumentLineType.Normal, quantityPerPost)]);
            documents.Add(doc);
        }

        // 2. Act: Post all documents concurrently
        var results = new ConcurrentBag<Result<PostingOutcome>>();
        IEnumerable<Task> tasks = documents.Select(async doc =>
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> result = await coordinator.PostAsync(doc.Id, doc.RowVersion, seed.ManagerUserId, CancellationToken.None);
            results.Add(result);
        });

        await Task.WhenAll(tasks);

        // 3. Assert: All posts must succeed without deadlock
        results.Count.ShouldBe(concurrentPosts);
        results.ShouldAllBe(r => r.IsSuccess);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            decimal finalBalance = await context.InventoryBalances
                .Where(b => b.WarehouseId == seed.WarehouseId && b.MaterialId == seed.NormalMaterialId)
                .Select(b => b.Quantity)
                .SingleAsync();

            finalBalance.ShouldBe(concurrentPosts * quantityPerPost);
        }
    }

    [Fact]
    public async Task ConcurrentPostingOfSameDocument_Should_CreateOneLedgerEntry()
    {
        // Arrange
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        WarehouseDocument document = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 10m)]);

        // Act
        Result<PostingOutcome>[] results = await Task.WhenAll(
            PostAsync(document, seed.ManagerUserId),
            PostAsync(document, seed.ManagerUserId));

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        results.Count(result => result.IsFailure).ShouldBe(1);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.StockMovements.CountAsync(movement => movement.DocumentId == document.Id)).ShouldBe(1);
        (await context.InventoryBalances
                .Where(balance =>
                    balance.WarehouseId == seed.WarehouseId &&
                    balance.MaterialId == seed.NormalMaterialId)
                .Select(balance => balance.Quantity)
                .SingleAsync())
            .ShouldBe(10m);
    }

    [Fact]
    public async Task ConcurrentIssues_Should_NotOversell_WhenOnlyOneCanConsumeTheRemainingBalance()
    {
        // Arrange
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        WarehouseDocument receiving = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 10m)]);
        (await PostAsync(receiving, seed.ManagerUserId)).IsSuccess.ShouldBeTrue();

        WarehouseDocument firstIssue = await CreateIssueAsync(seed, 10m);
        WarehouseDocument secondIssue = await CreateIssueAsync(seed, 10m);

        // Act
        Result<PostingOutcome>[] results = await Task.WhenAll(
            PostAsync(firstIssue, seed.ManagerUserId),
            PostAsync(secondIssue, seed.ManagerUserId));

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        Result<PostingOutcome> insufficientStock = results.Single(result => result.IsFailure);
        insufficientStock.Error.Code.ShouldBe("InventoryBalances.InsufficientQuantity");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.InventoryBalances
                .Where(balance =>
                    balance.WarehouseId == seed.WarehouseId &&
                    balance.MaterialId == seed.NormalMaterialId)
                .Select(balance => balance.Quantity)
                .SingleAsync())
            .ShouldBe(0m);
        (await context.StockMovements.CountAsync(movement =>
            movement.DocumentId == firstIssue.Id || movement.DocumentId == secondIssue.Id)).ShouldBe(1);
    }

    private async Task<WarehouseDocument> CreateIssueAsync(RegressionSeedData seed, decimal quantity) =>
        await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Issue,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, quantity)],
            (document, context) => context.IssueTos.Add(
                IssueTo.Create(
                    document.Id,
                    PartyType.OrganizationalUnit,
                    seed.OrgUnitId,
                    "Performance concurrency test").Value));

    private async Task<Result<PostingOutcome>> PostAsync(WarehouseDocument document, Guid postedBy)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = scope.ServiceProvider
            .GetRequiredService<IDocumentPostingCoordinator>();

        return await coordinator.PostAsync(
            document.Id,
            document.RowVersion,
            postedBy,
            CancellationToken.None);
    }
}
