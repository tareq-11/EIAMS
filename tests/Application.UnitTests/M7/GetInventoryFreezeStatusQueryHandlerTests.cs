using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.InventoryCounts;
using Application.InventoryCounts.GetFreezeStatus;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.Warehouses;
using SharedKernel;

namespace Application.UnitTests.M7;

public sealed class GetInventoryFreezeStatusQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnActiveFreezeState_WhenAuthorized()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var warehouseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var countId = Guid.NewGuid();
        context.Warehouses.Add(Warehouse.Create(
            warehouseId,
            Guid.NewGuid(),
            "Main warehouse",
            "WH-1",
            "Main",
            true));
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                userId,
                PermissionCodes.InventoryCounts.View,
                ScopeType.Warehouse,
                warehouseId,
                Arg.Any<CancellationToken>())
            .Returns(true);
        IInventoryFreezePolicyService freezeService = Substitute.For<IInventoryFreezePolicyService>();
        freezeService.EvaluateAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(new InventoryFreezeEvaluation(
                [new ActiveInventoryFreeze(countId, warehouseId, FreezePolicy.SoftFreeze)],
                [new InventoryFreezeWarning("InventoryCounts.SoftFreezeActive", "warning", countId, warehouseId)],
                null));
        var handler = new GetInventoryFreezeStatusQueryHandler(
            context,
            userContext,
            authorization,
            freezeService);

        // Act
        Result<InventoryFreezeStatusResponse> result = await handler.Handle(
            new GetInventoryFreezeStatusQuery(warehouseId),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsPostingBlocked.ShouldBeFalse();
        result.Value.HasSoftFreezeWarning.ShouldBeTrue();
        result.Value.ActiveCounts.ShouldHaveSingleItem().CountId.ShouldBe(countId);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenScopeIsUnauthorized()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var warehouseId = Guid.NewGuid();
        context.Warehouses.Add(Warehouse.Create(
            warehouseId,
            Guid.NewGuid(),
            "Main warehouse",
            "WH-1",
            "Main",
            true));
        await context.SaveChangesAsync();
        IUserContext userContext = Substitute.For<IUserContext>();
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<ScopeType>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(false);
        IInventoryFreezePolicyService freezeService = Substitute.For<IInventoryFreezePolicyService>();
        var handler = new GetInventoryFreezeStatusQueryHandler(
            context,
            userContext,
            authorization,
            freezeService);

        // Act
        Result<InventoryFreezeStatusResponse> result = await handler.Handle(
            new GetInventoryFreezeStatusQuery(warehouseId),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(WarehouseErrors.NotFound(warehouseId));
        await freezeService.DidNotReceiveWithAnyArgs()
            .EvaluateAsync(default!, default);
    }
}
