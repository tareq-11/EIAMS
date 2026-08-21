using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryCounts.GetList;

internal sealed class GetInventoryCountsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetInventoryCountsQuery, PagedResult<InventoryCountResponse>>
{
    public async Task<Result<PagedResult<InventoryCountResponse>>> Handle(
        GetInventoryCountsQuery query,
        CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.InventoryCounts.View,
            ScopeType.Warehouse,
            query.WarehouseId,
            cancellationToken);

        if (!authorized || !await context.Warehouses.AsNoTracking()
                .AnyAsync(item => item.Id == query.WarehouseId, cancellationToken))
        {
            return Result.Failure<PagedResult<InventoryCountResponse>>(
                WarehouseErrors.NotFound(query.WarehouseId));
        }

        PagedResult<InventoryCountResponse> result = await context.InventoryCounts
            .AsNoTracking()
            .Where(item => item.WarehouseId == query.WarehouseId)
            .Where(item => query.Status == null || item.Status == query.Status)
            .Select(item => new InventoryCountResponse(
                item.Id,
                item.WarehouseId,
                item.CountType,
                item.ScopeType,
                item.FreezePolicy,
                item.Status,
                item.RowVersion,
                item.PlannedAtUtc,
                item.StartedAtUtc,
                item.CompletedAtUtc,
                item.ClosedAtUtc))
            .OrderByDescending(item => item.PlannedAtUtc)
            .ThenBy(item => item.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return result;
    }
}
