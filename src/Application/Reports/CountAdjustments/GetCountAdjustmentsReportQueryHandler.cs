using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reports.CountAdjustments;

internal sealed class GetCountAdjustmentsReportQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetCountAdjustmentsReportQuery, PagedResult<CountAdjustmentsReportRow>>
{
    public async Task<Result<PagedResult<CountAdjustmentsReportRow>>> Handle(
        GetCountAdjustmentsReportQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId, PermissionCodes.InventoryCounts.View, cancellationToken);
        bool canViewDocuments = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.View, cancellationToken);
        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0 || !canViewDocuments)
        {
            return Result.Failure<PagedResult<CountAdjustmentsReportRow>>(ReportErrors.Forbidden);
        }

        Guid[] warehouseIds = access.WarehouseIds.ToArray();
        DateTime? fromDate = query.FromUtc?.UtcDateTime;
        DateTime? toDate = query.ToUtc?.UtcDateTime;
        IQueryable<CountAdjustmentsReportRow> source =
            from warehouse in context.Warehouses.AsNoTracking()
            where access.HasEnterpriseAccess || warehouseIds.Contains(warehouse.Id)
            where query.WarehouseId == null || warehouse.Id == query.WarehouseId
            orderby warehouse.Code, warehouse.Id
            select new CountAdjustmentsReportRow(
                warehouse.Id,
                warehouse.Code,
                warehouse.Name,
                context.InventoryCounts.Count(count => count.WarehouseId == warehouse.Id &&
                    (fromDate == null || count.CreatedAtUtc >= fromDate) &&
                    (toDate == null || count.CreatedAtUtc < toDate)),
                context.InventoryCounts.Count(count => count.WarehouseId == warehouse.Id &&
                    count.Status == InventoryCountStatus.Closed &&
                    (fromDate == null || count.CreatedAtUtc >= fromDate) &&
                    (toDate == null || count.CreatedAtUtc < toDate)),
                (from line in context.InventoryCountLines
                 join count in context.InventoryCounts on line.CountId equals count.Id
                 where count.WarehouseId == warehouse.Id && line.Difference != null && line.Difference != 0 &&
                       (fromDate == null || count.CreatedAtUtc >= fromDate) &&
                       (toDate == null || count.CreatedAtUtc < toDate)
                 select line).Count(),
                (from line in context.InventoryCountLines
                 join count in context.InventoryCounts on line.CountId equals count.Id
                 where count.WarehouseId == warehouse.Id &&
                       (fromDate == null || count.CreatedAtUtc >= fromDate) &&
                       (toDate == null || count.CreatedAtUtc < toDate)
                 select line.Difference).Sum(value => value ?? 0m),
                (from adjustment in context.InventoryAdjustments
                 join document in context.WarehouseDocuments on adjustment.Id equals document.Id
                 where document.WarehouseId == warehouse.Id &&
                       (fromDate == null || adjustment.CreatedAtUtc >= fromDate) &&
                       (toDate == null || adjustment.CreatedAtUtc < toDate)
                 select adjustment).Count(),
                (from adjustment in context.InventoryAdjustments
                 join document in context.WarehouseDocuments on adjustment.Id equals document.Id
                 where document.WarehouseId == warehouse.Id && adjustment.Status == InventoryAdjustmentStatus.Posted &&
                       (fromDate == null || adjustment.CreatedAtUtc >= fromDate) &&
                       (toDate == null || adjustment.CreatedAtUtc < toDate)
                 select adjustment).Count(),
                (from line in context.AdjustmentLines
                 join adjustment in context.InventoryAdjustments on line.AdjustmentId equals adjustment.Id
                 join document in context.WarehouseDocuments on adjustment.Id equals document.Id
                 where document.WarehouseId == warehouse.Id &&
                       (fromDate == null || adjustment.CreatedAtUtc >= fromDate) &&
                       (toDate == null || adjustment.CreatedAtUtc < toDate)
                 select line.Difference).Sum());

        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<CountAdjustmentsReportRow> items = await source.Skip(offset).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<CountAdjustmentsReportRow>(items, query.Page, query.PageSize, totalItems);
    }
}
