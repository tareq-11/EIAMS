using System.Diagnostics;
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
public sealed class AuditOverheadTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public AuditOverheadTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AuditCapture_Should_CaptureAuditEntriesWithinAcceptableTime()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        WarehouseDocument doc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 5m)]);

        // 2. Act: Measure posting with audit capture
        var stopwatch = Stopwatch.StartNew();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> result = await coordinator.PostAsync(doc.Id, doc.RowVersion, seed.ManagerUserId, CancellationToken.None);
            result.IsSuccess.ShouldBeTrue();
        }

        stopwatch.Stop();

        // 3. Assert: Posting must complete well within threshold (< 5 seconds for local test environment)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000);

        // Verify audit log exists
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            int auditLogCount = await context.AuditLogs.Where(a => a.EntityId == doc.Id).CountAsync();
            auditLogCount.ShouldBeGreaterThan(0);
        }
    }
}
