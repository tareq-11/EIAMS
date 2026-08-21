using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.InventoryCounts;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryCounts.GetById;

internal sealed class GetInventoryCountByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetInventoryCountByIdQuery, InventoryCountDetailsResponse>
{
    public async Task<Result<InventoryCountDetailsResponse>> Handle(GetInventoryCountByIdQuery query, CancellationToken cancellationToken)
    {
        InventoryCount? count = await context.InventoryCounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == query.CountId, cancellationToken);
        if (count is null || !await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.InventoryCounts.View, ScopeType.Warehouse, count.WarehouseId, cancellationToken))
        {
            return Result.Failure<InventoryCountDetailsResponse>(InventoryCountErrors.NotFound(query.CountId));
        }

        IQueryable<InventoryCountLine> lines = context.InventoryCountLines.AsNoTracking()
            .Where(item => item.CountId == count.Id);
        int totalLines = await lines.CountAsync(cancellationToken);
        int countedLines = await lines.CountAsync(item => item.ActualQuantity != null, cancellationToken);
        int varianceLines = await lines.CountAsync(
            item => item.Difference != null && item.Difference != 0, cancellationToken);
        decimal totalAbsoluteDifference = await lines
            .SumAsync(item => Math.Abs(item.Difference.GetValueOrDefault()),
                cancellationToken);
        var summary = new InventoryCountSummaryResponse(
            totalLines, countedLines, varianceLines, totalAbsoluteDifference);

        return new InventoryCountDetailsResponse(count.Id, count.WarehouseId, count.CountType, count.ScopeType,
            count.ScopeMaterialDomainId, count.FreezePolicy, count.Status, count.RowVersion,
            count.PlannedAtUtc, count.StartedAtUtc, count.CompletedAtUtc, count.ClosedAtUtc, summary);
    }
}
