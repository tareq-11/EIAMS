using Application.Abstractions.Posting;
using Application.Abstractions.Warehouses;
using Domain.Common;
using Domain.DocumentLines;
using Domain.ReceivingInfos;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using IntegrationTests.Regression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Authorization;

[Collection(nameof(IntegrationTestCollection))]
public sealed class CapabilityGateTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public CapabilityGateTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData(OperationType.Receiving)]
    [InlineData(OperationType.Issue)]
    [InlineData(OperationType.Transfer)]
    [InlineData(OperationType.Count)]
    [InlineData(OperationType.Return)]
    [InlineData(OperationType.Adjustment)]
    public async Task CapabilityService_Should_Reject_EachMissingOperation(OperationType operationType)
    {
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseCapability capability = await context.WarehouseCapabilities.SingleAsync(item =>
            item.WarehouseId == seed.WarehouseId && item.MaterialDomainId == seed.DomainId);
        WarehouseCapabilityOperation operation = await context.WarehouseCapabilityOperations.SingleAsync(item =>
            item.CapabilityId == capability.Id && item.OperationType == operationType);
        context.WarehouseCapabilityOperations.Remove(operation);
        await context.SaveChangesAsync();

        ICapabilityCheckService service = scope.ServiceProvider.GetRequiredService<ICapabilityCheckService>();
        Result result = await service.EnsureAllowedBatchAsync(
            seed.WarehouseId,
            [seed.DomainId],
            operationType,
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("WarehouseCapabilityOperations.OperationNotGranted");
    }

    [Fact]
    public async Task Post_Should_Fail_When_WarehouseLacksCapabilityForDomain()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // Remove capabilities for wh1
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            List<WarehouseCapability> capabilities = await context.WarehouseCapabilities.Where(c => c.WarehouseId == seed.WarehouseId).ToListAsync();
            Guid[] capabilityIds = capabilities.Select(c => c.Id).ToArray();
            List<WarehouseCapabilityOperation> operations = await context.WarehouseCapabilityOperations
                .Where(operation => capabilityIds.Contains(operation.CapabilityId))
                .ToListAsync();
            context.WarehouseCapabilityOperations.RemoveRange(operations);
            context.WarehouseCapabilities.RemoveRange(capabilities);
            await context.SaveChangesAsync();
        }

        // 2. Create and Submit Receiving Document
        WarehouseDocument doc = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 5m)],
            (document, context) => context.ReceivingInfos.Add(
                ReceivingInfo.Create(document.Id, "Capability test supplier", null, ReceivingType.Supplier).Value));

        // 3. Post should fail due to missing capability
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> postResult = await coordinator.PostAsync(doc.Id, doc.RowVersion, seed.ManagerUserId, CancellationToken.None);

            postResult.IsFailure.ShouldBeTrue();
            postResult.Error.Code.ShouldBe("WarehouseCapabilities.NotGranted");
        }
    }
}
