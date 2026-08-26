using System.Collections.Concurrent;
using System.Diagnostics;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using IntegrationTests.Regression;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Observability;

[Collection(nameof(IntegrationTestCollection))]
public sealed class PostingTracingTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public PostingTracingTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Posting_Should_EmitDocumentActivity_WithDocumentTags()
    {
        // Arrange
        var stoppedActivities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "CleanArchitecture.DocumentPosting",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Add
        };
        ActivitySource.AddActivityListener(listener);

        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        WarehouseDocument document = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 1m)]);

        // Act
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = scope.ServiceProvider
            .GetRequiredService<IDocumentPostingCoordinator>();
        Result<PostingOutcome> result = await coordinator.PostAsync(
            document.Id,
            document.RowVersion,
            seed.ManagerUserId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Activity activity = stoppedActivities.Single(item => item.OperationName == "PostDocument");
        activity.GetTagItem("document.id").ShouldBe(document.Id);
        activity.GetTagItem("posted_by.id").ShouldBe(seed.ManagerUserId);
    }
}
