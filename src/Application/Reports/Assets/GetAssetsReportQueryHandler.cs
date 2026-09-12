using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reports.Assets;

internal sealed class GetAssetsReportQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetAssetsReportQuery, PagedResult<AssetsReportRow>>
{
    public async Task<Result<PagedResult<AssetsReportRow>>> Handle(
        GetAssetsReportQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId, PermissionCodes.Assets.View, cancellationToken);
        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<AssetsReportRow>>(ReportErrors.Forbidden);
        }

        Guid[] warehouseIds = access.WarehouseIds.ToArray();
        IQueryable<AssetsReportRow> source =
            from asset in context.Assets.AsNoTracking()
            join current in context.AssetCurrentStatuses.AsNoTracking() on asset.Id equals current.AssetId
            join warehouse in context.Warehouses.AsNoTracking() on asset.WarehouseId equals warehouse.Id
            where asset.WarehouseId != null
            where access.HasEnterpriseAccess || warehouseIds.Contains(warehouse.Id)
            where query.WarehouseId == null || asset.WarehouseId == query.WarehouseId
            where query.Status == null || current.CurrentStatus == query.Status
            group asset by new { warehouse.Id, warehouse.Code, warehouse.Name, current.CurrentStatus } into grouped
            orderby grouped.Key.Code, grouped.Key.CurrentStatus, grouped.Key.Id
            select new AssetsReportRow(
                grouped.Key.Id,
                grouped.Key.Code,
                grouped.Key.Name,
                grouped.Key.CurrentStatus.ToString(),
                grouped.Count());

        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<AssetsReportRow> items = await source.Skip(offset).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<AssetsReportRow>(items, query.Page, query.PageSize, totalItems);
    }
}
