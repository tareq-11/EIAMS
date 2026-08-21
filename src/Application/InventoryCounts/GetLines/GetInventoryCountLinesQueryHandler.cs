using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.InventoryCounts;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryCounts.GetLines;

internal sealed class GetInventoryCountLinesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetInventoryCountLinesQuery, PagedResult<InventoryCountLineResponse>>
{
    public async Task<Result<PagedResult<InventoryCountLineResponse>>> Handle(
        GetInventoryCountLinesQuery query,
        CancellationToken cancellationToken)
    {
        InventoryCount? count = await context.InventoryCounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == query.CountId, cancellationToken);
        if (count is null || !await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.InventoryCounts.View,
            ScopeType.Warehouse,
            count.WarehouseId,
            cancellationToken))
        {
            return Result.Failure<PagedResult<InventoryCountLineResponse>>(
                InventoryCountErrors.NotFound(query.CountId));
        }

        PagedResult<InventoryCountLineResponse> result = await context.InventoryCountLines
            .AsNoTracking()
            .Where(item => item.CountId == count.Id)
            .Where(item => !query.OnlyVariance || item.Difference != null && item.Difference != 0)
            .Select(item => new InventoryCountLineResponse(
                item.Id,
                item.MaterialId,
                item.AssetId,
                item.SnapshotQuantity,
                item.ActualQuantity,
                item.Difference,
                item.VarianceReason))
            .OrderBy(item => item.MaterialId)
            .ThenBy(item => item.AssetId)
            .ThenBy(item => item.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return result;
    }
}
