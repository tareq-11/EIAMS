using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.InventoryCounts.SetVarianceReason;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.InventoryCounts;
using SharedKernel;

namespace Application.UnitTests.M7;

public sealed class SetInventoryCountVarianceReasonCommandHandlerTests : BaseHandlerTest
{
    [Theory]
    [InlineData(InventoryCountStatus.InProgress)]
    [InlineData(InventoryCountStatus.Completed)]
    public async Task Handle_Should_SetReason_WhenCountAcceptsVarianceReview(InventoryCountStatus status)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        DateTime nowUtc = DateTime.UtcNow;
        InventoryCount count = InventoryCount.Plan(
            Guid.NewGuid(), Guid.NewGuid(), userId, InventoryCountType.Scheduled,
            InventoryCountScopeType.EntireWarehouse, null, FreezePolicy.NoFreeze, nowUtc).Value;
        count.Start(nowUtc.AddSeconds(1));
        InventoryCountLine line = InventoryCountLine.Create(
            Guid.NewGuid(), count.Id, Guid.NewGuid(), null, 2m).Value;
        line.RecordActual(1m);
        if (status == InventoryCountStatus.Completed)
        {
            count.Complete(nowUtc.AddSeconds(2));
        }
        context.InventoryCounts.Add(count);
        context.InventoryCountLines.Add(line);
        await context.SaveChangesAsync();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                userId, PermissionCodes.InventoryCounts.Review, ScopeType.Warehouse,
                count.WarehouseId, Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new SetInventoryCountVarianceReasonCommandHandler(context, userContext, authorization);
        var command = new SetInventoryCountVarianceReasonCommand(
            count.Id, line.Id, "  recount confirmed  ", count.RowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        line.VarianceReason.ShouldBe("recount confirmed");
        count.RowVersion.ShouldBe(status == InventoryCountStatus.InProgress ? 3 : 4);
    }
}
