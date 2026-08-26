using System.Collections.Concurrent;
using Application.Abstractions.Posting;
using Domain.Common;
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
}
