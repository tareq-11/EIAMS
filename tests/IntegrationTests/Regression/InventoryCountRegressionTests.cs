using Domain.Common;
using Domain.InventoryCounts;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Regression;

[Collection(nameof(IntegrationTestCollection))]
public sealed class InventoryCountRegressionTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public InventoryCountRegressionTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task InventoryCountFlow_HappyPath_Should_ProgressThroughAllStages_WithoutMutatingStock()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        DateTime now = DateTime.UtcNow;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 2. Stage 1: Plan Count
        Result<InventoryCount> countResult = InventoryCount.Plan(
            Guid.NewGuid(),
            seed.WarehouseId,
            seed.ManagerUserId,
            InventoryCountType.Scheduled,
            InventoryCountScopeType.EntireWarehouse,
            null,
            FreezePolicy.SoftFreeze,
            now);

        InventoryCount count = countResult.Value;
        context.InventoryCounts.Add(count);

        // Add count line with snapshot = 10, actual = 8, diff = -2
        Result<InventoryCountLine> lineResult = InventoryCountLine.Create(
            Guid.NewGuid(),
            count.Id,
            seed.NormalMaterialId,
            null,
            10m);

        InventoryCountLine countLine = lineResult.Value;
        countLine.RecordActual(8m);
        countLine.SetVarianceReason("Physical shortage");

        context.InventoryCountLines.Add(countLine);
        await context.SaveChangesAsync();

        count.Status.ShouldBe(InventoryCountStatus.Planned);

        // 3. Stage 2: Start Count
        Result startResult = count.Start(now.AddMinutes(5));
        startResult.IsSuccess.ShouldBeTrue();
        count.Status.ShouldBe(InventoryCountStatus.InProgress);
        await context.SaveChangesAsync();

        // 4. Stage 3: Complete Count
        Result completeResult = count.Complete(now.AddMinutes(30));
        completeResult.IsSuccess.ShouldBeTrue();
        count.Status.ShouldBe(InventoryCountStatus.Completed);
        await context.SaveChangesAsync();

        // 5. Stage 4: Close Count
        Result closeResult = count.Close(now.AddHours(1));
        closeResult.IsSuccess.ShouldBeTrue();
        count.Status.ShouldBe(InventoryCountStatus.Closed);
        await context.SaveChangesAsync();

        // Verify count itself does NOT create movements or change stock
        int movementCount = await context.StockMovements.CountAsync(movement =>
            movement.WarehouseId == seed.WarehouseId);
        movementCount.ShouldBe(0);

        InventoryCount closedCount = await context.InventoryCounts.SingleAsync(c => c.Id == count.Id);
        closedCount.Status.ShouldBe(InventoryCountStatus.Closed);
    }
}
