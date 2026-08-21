using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.InventoryCounts;
using Application.Abstractions.Warehouses;
using Application.InventoryCounts.Plan;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.Warehouses;
using SharedKernel;

namespace Application.UnitTests.M7;

public sealed class PlanInventoryCountCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_LockAndPersistSelectedSnapshot_InsideTransaction()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        context.Warehouses.Add(Warehouse.Create(
            warehouseId, Guid.NewGuid(), "Warehouse", "WH-COUNT", "General", true));
        context.MaterialDomains.Add(MaterialDomain.Create(domainId, "Medical", "MED"));
        context.MaterialCategories.Add(MaterialCategory.Create(
            categoryId, domainId, null, "Category", "CAT"));
        context.MaterialFamilies.Add(MaterialFamily.Create(
            familyId, categoryId, "Family", "FAM", Guid.NewGuid()));
        context.Materials.Add(Material.Create(
            materialId, familyId, "مادة", "Material", "MAT-COUNT",
            MaterialKind.Consumable, TrackingType.Quantity, false, false, null));
        await context.SaveChangesAsync();

        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<Result<Guid>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result<Guid>>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        IWarehouseOperationLock warehouseLock = Substitute.For<IWarehouseOperationLock>();
        warehouseLock.AcquireAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                userId, PermissionCodes.InventoryCounts.Plan, ScopeType.Warehouse,
                warehouseId, Arg.Any<CancellationToken>())
            .Returns(true);
        ICapabilityCheckService capability = Substitute.For<ICapabilityCheckService>();
        capability.EnsureAllowedAsync(
                warehouseId, domainId, OperationType.Count, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        var handler = new PlanInventoryCountCommandHandler(
            context, userContext, authorization, capability, transaction, warehouseLock, dateTimeProvider);
        var command = new PlanInventoryCountCommand(
            warehouseId, InventoryCountType.Cycle, InventoryCountScopeType.SelectedMaterials,
            null, [materialId], FreezePolicy.HardFreeze);

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        context.InventoryCounts.ShouldContain(item => item.Id == result.Value);
        context.InventoryCountLines.ShouldContain(item =>
            item.CountId == result.Value && item.MaterialId == materialId && item.SnapshotQuantity == 0m);
        context.InventoryCountScopeMaterials.ShouldContain(item =>
            item.CountId == result.Value && item.MaterialId == materialId);
        await warehouseLock.Received(1).AcquireAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { warehouseId })),
            Arg.Any<CancellationToken>());
        await transaction.Received(1).ExecuteAsync(
            Arg.Any<Func<CancellationToken, Task<Result<Guid>>>>(),
            Arg.Any<CancellationToken>());
    }
}
