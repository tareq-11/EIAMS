using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reports.Dashboard;

internal sealed class GetDashboardReportQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetDashboardReportQuery, DashboardReportResponse>
{
    public async Task<Result<DashboardReportResponse>> Handle(
        GetDashboardReportQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId, PermissionCodes.Inventory.View, cancellationToken);
        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<DashboardReportResponse>(ReportErrors.Forbidden);
        }

        Guid[] warehouseIds = access.WarehouseIds.ToArray();
        IQueryable<Guid> warehouses = context.Warehouses.AsNoTracking()
            .Where(warehouse => access.HasEnterpriseAccess || warehouseIds.Contains(warehouse.Id))
            .Where(warehouse => query.WarehouseId == null || warehouse.Id == query.WarehouseId)
            .Select(warehouse => warehouse.Id);
        bool canViewAssets = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId, PermissionCodes.Assets.View, cancellationToken);
        bool canViewCustodies = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId, PermissionCodes.Custodies.View, cancellationToken);
        bool canViewDocuments = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.View, cancellationToken);
        bool canViewCounts = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId, PermissionCodes.InventoryCounts.View, cancellationToken);

        // These aggregates are independent but previously incurred seven sequential database
        // round trips. A single projection keeps the exact same scoped subqueries while making
        // the dashboard's data collection one database command.
        DashboardMetrics? metrics = await warehouses
            .GroupBy(_ => 1)
            .Select(_ => new DashboardMetrics(
                warehouses.Count(),
                context.InventoryBalances.AsNoTracking()
                    .Where(balance => warehouses.Contains(balance.WarehouseId) && balance.Quantity > 0)
                    .Select(balance => balance.MaterialId)
                    .Distinct()
                    .Count(),
                context.InventoryBalances.AsNoTracking()
                    .Where(balance => warehouses.Contains(balance.WarehouseId))
                    .Sum(balance => (decimal?)balance.Quantity) ?? 0m,
                canViewAssets
                    ? context.Assets.AsNoTracking().Count(asset =>
                        asset.WarehouseId != null && warehouses.Contains(asset.WarehouseId.Value))
                    : null,
                canViewCustodies
                    ? context.Custodies.AsNoTracking()
                        .Where(custody => custody.Status == CustodyStatus.Active)
                        .Join(context.Assets.AsNoTracking(), custody => custody.AssetId, asset => asset.Id, (_, asset) => asset)
                        .Count(asset => asset.WarehouseId != null && warehouses.Contains(asset.WarehouseId.Value))
                    : null,
                canViewDocuments
                    ? context.WarehouseDocuments.AsNoTracking().Count(document =>
                        warehouses.Contains(document.WarehouseId) &&
                        (document.DocumentStatus == DocumentStatus.Draft ||
                         document.DocumentStatus == DocumentStatus.Submitted))
                    : null,
                canViewCounts
                    ? context.InventoryCounts.AsNoTracking().Count(count =>
                        warehouses.Contains(count.WarehouseId) &&
                        (count.Status == InventoryCountStatus.Planned ||
                         count.Status == InventoryCountStatus.InProgress ||
                         count.Status == InventoryCountStatus.Completed))
                    : null))
            .SingleOrDefaultAsync(cancellationToken);

        metrics ??= DashboardMetrics.Empty(
            canViewAssets,
            canViewCustodies,
            canViewDocuments,
            canViewCounts);

        return new DashboardReportResponse(
            metrics.WarehouseCount,
            metrics.StockedMaterialCount,
            metrics.TotalOnHandQuantity,
            metrics.AssetCount,
            metrics.ActiveCustodyCount,
            metrics.OpenDocumentCount,
            metrics.ActiveInventoryCountCount,
            dateTimeProvider.UtcNow);
    }

    private sealed record DashboardMetrics(
        int WarehouseCount,
        int StockedMaterialCount,
        decimal TotalOnHandQuantity,
        int? AssetCount,
        int? ActiveCustodyCount,
        int? OpenDocumentCount,
        int? ActiveInventoryCountCount)
    {
        internal static DashboardMetrics Empty(
            bool canViewAssets,
            bool canViewCustodies,
            bool canViewDocuments,
            bool canViewCounts) => new(
            0,
            0,
            0m,
            canViewAssets ? 0 : null,
            canViewCustodies ? 0 : null,
            canViewDocuments ? 0 : null,
            canViewCounts ? 0 : null);
    }
}
