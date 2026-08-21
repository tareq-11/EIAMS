using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.InventoryCounts.RecordActualBatch;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.InventoryCounts;
using SharedKernel;

namespace Application.UnitTests.M7;

public sealed class RecordInventoryCountActualsCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_RecordAllActualsAtomically_WhenRequestIsValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        InventoryCount count = CreateStartedCount(userId);
        InventoryCountLine first = InventoryCountLine.Create(
            Guid.NewGuid(), count.Id, Guid.NewGuid(), null, 10m).Value;
        InventoryCountLine second = InventoryCountLine.Create(
            Guid.NewGuid(), count.Id, Guid.NewGuid(), null, 4m).Value;
        context.InventoryCounts.Add(count);
        context.InventoryCountLines.AddRange(first, second);
        await context.SaveChangesAsync();
        RecordInventoryCountActualsCommandHandler handler = CreateHandler(context, userId, authorized: true);
        RecordInventoryCountActualsCommand command = new(
            count.Id,
            [new(first.Id, 8m), new(second.Id, 5m)],
            count.RowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        first.ActualQuantity.ShouldBe(8m);
        first.Difference.ShouldBe(-2m);
        second.ActualQuantity.ShouldBe(5m);
        second.Difference.ShouldBe(1m);
        count.RowVersion.ShouldBe(3);
        first.DomainEvents.ShouldContain(item => item is InventoryCountActualRecordedDomainEvent);
        second.DomainEvents.ShouldContain(item => item is InventoryCountActualRecordedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_NotMutateAnyLine_WhenOneLineDoesNotBelongToCount()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        InventoryCount count = CreateStartedCount(userId);
        InventoryCountLine line = InventoryCountLine.Create(
            Guid.NewGuid(), count.Id, Guid.NewGuid(), null, 10m).Value;
        context.InventoryCounts.Add(count);
        context.InventoryCountLines.Add(line);
        await context.SaveChangesAsync();
        RecordInventoryCountActualsCommandHandler handler = CreateHandler(context, userId, authorized: true);
        var missingLineId = Guid.NewGuid();
        RecordInventoryCountActualsCommand command = new(
            count.Id,
            [new(line.Id, 8m), new(missingLineId, 2m)],
            count.RowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(InventoryCountLineErrors.NotFound(missingLineId));
        line.ActualQuantity.ShouldBeNull();
        count.RowVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenScopeIsUnauthorized()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        InventoryCount count = CreateStartedCount(userId);
        context.InventoryCounts.Add(count);
        await context.SaveChangesAsync();
        RecordInventoryCountActualsCommandHandler handler = CreateHandler(context, userId, authorized: false);
        RecordInventoryCountActualsCommand command = new(
            count.Id, [new(Guid.NewGuid(), 1m)], count.RowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(InventoryCountErrors.NotFound(count.Id));
    }

    private static InventoryCount CreateStartedCount(Guid userId)
    {
        DateTime plannedAtUtc = DateTime.UtcNow;
        InventoryCount count = InventoryCount.Plan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            userId,
            InventoryCountType.Scheduled,
            InventoryCountScopeType.EntireWarehouse,
            null,
            FreezePolicy.NoFreeze,
            plannedAtUtc).Value;
        count.Start(plannedAtUtc.AddSeconds(1));
        return count;
    }

    private static RecordInventoryCountActualsCommandHandler CreateHandler(
        TestDbContext context,
        Guid userId,
        bool authorized)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                userId,
                PermissionCodes.InventoryCounts.EnterActual,
                ScopeType.Warehouse,
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(authorized);
        return new RecordInventoryCountActualsCommandHandler(context, userContext, authorization);
    }
}
