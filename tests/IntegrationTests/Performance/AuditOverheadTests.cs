using System.Diagnostics;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using IntegrationTests.Regression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuditOverheadTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;
    private readonly ITestOutputHelper output;

    public AuditOverheadTests(
        IntegrationTestWebAppFactory factory,
        ITestOutputHelper output) : base(factory)
    {
        this.factory = factory;
        this.output = output;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task AuditCapture_Should_RecordEveryPostedMovement_ForNormalLineCounts(int lineCount)
    {
        // Arrange
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        WarehouseDocument doc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            Enumerable.Range(0, lineCount)
                .Select(_ => (seed.NormalMaterialId, DocumentLineType.Normal, 1m))
                .ToList());

        // Act
        var stopwatch = Stopwatch.StartNew();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> result = await coordinator.PostAsync(doc.Id, doc.RowVersion, seed.ManagerUserId, CancellationToken.None);
            result.IsSuccess.ShouldBeTrue();
        }

        stopwatch.Stop();

        // Assert
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            int movementAuditLogCount = await context.AuditLogs.CountAsync(log =>
                log.EntityType == "StockMovement" &&
                log.AggregateId == doc.Id);
            movementAuditLogCount.ShouldBe(lineCount);
        }

        output.WriteLine(
            $"Audit posting measurement: lines={lineCount}, elapsed={stopwatch.ElapsedMilliseconds}ms.");
    }

    [ExplicitPerformanceFact]
    [Trait("Category", "Performance")]
    public Task AuditCapture_Should_RecordEveryPostedMovement_ForOneThousandLines() =>
        AuditCapture_Should_RecordEveryPostedMovement_ForNormalLineCounts(1000);

}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitPerformanceFactAttribute : FactAttribute
{
    public ExplicitPerformanceFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_PERFORMANCE_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Explicit performance test. Set RUN_PERFORMANCE_TESTS=1 to include the 1,000-line audit measurement.";
        }
    }
}
