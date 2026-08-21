using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Pagination;
using Application.InventoryCounts.GetById;
using Application.InventoryCounts.GetLines;
using Application.InventoryCounts.GetList;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.InventoryCounts;
using Domain.Warehouses;
using SharedKernel;

namespace Application.UnitTests.M7;

public sealed class InventoryCountQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Queries_Should_ReturnDeterministicPaginationAndSummary_WhenAuthorized()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        context.Warehouses.Add(Warehouse.Create(
            warehouseId, Guid.NewGuid(), "Warehouse", "WH-QUERY", "General", true));
        InventoryCount count = InventoryCount.Plan(
            Guid.NewGuid(), warehouseId, userId, InventoryCountType.Cycle,
            InventoryCountScopeType.EntireWarehouse, null, FreezePolicy.NoFreeze, DateTime.UtcNow).Value;
        InventoryCountLine unchanged = InventoryCountLine.Create(
            Guid.NewGuid(), count.Id, Guid.NewGuid(), null, 2m).Value;
        unchanged.RecordActual(2m);
        InventoryCountLine variance = InventoryCountLine.Create(
            Guid.NewGuid(), count.Id, Guid.NewGuid(), null, 5m).Value;
        variance.RecordActual(3m);
        context.InventoryCounts.Add(count);
        context.InventoryCountLines.AddRange(unchanged, variance);
        await context.SaveChangesAsync();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                userId, PermissionCodes.InventoryCounts.View, ScopeType.Warehouse,
                warehouseId, Arg.Any<CancellationToken>())
            .Returns(true);
        var listHandler = new GetInventoryCountsQueryHandler(context, userContext, authorization);
        var linesHandler = new GetInventoryCountLinesQueryHandler(context, userContext, authorization);
        var detailsHandler = new GetInventoryCountByIdQueryHandler(context, userContext, authorization);

        // Act
        Result<PagedResult<InventoryCountResponse>> list = await listHandler.Handle(
            new GetInventoryCountsQuery(warehouseId, InventoryCountStatus.Planned, 1, 20),
            CancellationToken.None);
        Result<PagedResult<Application.InventoryCounts.GetLines.InventoryCountLineResponse>> lines =
            await linesHandler.Handle(
                new GetInventoryCountLinesQuery(count.Id, true, 1, 1), CancellationToken.None);
        Result<InventoryCountDetailsResponse> details = await detailsHandler.Handle(
            new GetInventoryCountByIdQuery(count.Id), CancellationToken.None);

        // Assert
        list.IsSuccess.ShouldBeTrue();
        list.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(count.Id);
        lines.IsSuccess.ShouldBeTrue();
        lines.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(variance.Id);
        lines.Value.TotalItems.ShouldBe(1);
        details.IsSuccess.ShouldBeTrue();
        details.Value.Summary.TotalLines.ShouldBe(2);
        details.Value.Summary.CountedLines.ShouldBe(2);
        details.Value.Summary.VarianceLines.ShouldBe(1);
        details.Value.Summary.TotalAbsoluteDifference.ShouldBe(2m);
    }
}
