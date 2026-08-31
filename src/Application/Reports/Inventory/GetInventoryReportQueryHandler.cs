using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reports.Inventory;

internal sealed class GetInventoryReportQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetInventoryReportQuery, PagedResult<InventoryReportRow>>
{
    public async Task<Result<PagedResult<InventoryReportRow>>> Handle(
        GetInventoryReportQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId, PermissionCodes.Inventory.View, cancellationToken);
        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<InventoryReportRow>>(ReportErrors.Forbidden);
        }

        Guid[] warehouseIds = access.WarehouseIds.ToArray();
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().ToUpperInvariant();
        IQueryable<InventoryReportRow> source =
            from balance in context.InventoryBalances.AsNoTracking()
            join material in context.Materials.AsNoTracking() on balance.MaterialId equals material.Id
            where access.HasEnterpriseAccess || warehouseIds.Contains(balance.WarehouseId)
            where query.WarehouseId == null || balance.WarehouseId == query.WarehouseId
            where query.MaterialId == null || balance.MaterialId == query.MaterialId
#pragma warning disable CA1304, CA1311 // Translated by EF Core into SQL UPPER.
            where search == null || EF.Functions.Like(material.Code.ToUpper(), $"%{search}%") ||
                  EF.Functions.Like(material.NameAr.ToUpper(), $"%{search}%")
#pragma warning restore CA1304, CA1311
            group balance by new { material.Id, material.Code, material.NameAr } into grouped
            orderby grouped.Key.Code, grouped.Key.Id
            select new InventoryReportRow(
                grouped.Key.Id,
                grouped.Key.Code,
                grouped.Key.NameAr,
                grouped.Sum(balance => balance.Quantity),
                grouped.Select(balance => balance.WarehouseId).Distinct().Count(),
                grouped.Max(balance => balance.LastUpdatedUtc));

        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<InventoryReportRow> items = await source.Skip(offset).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<InventoryReportRow>(items, query.Page, query.PageSize, totalItems);
    }
}
