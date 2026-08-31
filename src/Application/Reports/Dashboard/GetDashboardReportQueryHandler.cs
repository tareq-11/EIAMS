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
        int warehouseCount = await warehouses.CountAsync(cancellationToken);
        int stockedMaterialCount = await context.InventoryBalances.AsNoTracking()
            .Where(balance => warehouses.Contains(balance.WarehouseId) && balance.Quantity > 0)
            .Select(balance => balance.MaterialId)
            .Distinct()
            .CountAsync(cancellationToken);
        decimal totalOnHand = await context.InventoryBalances.AsNoTracking()
            .Where(balance => warehouses.Contains(balance.WarehouseId))
            .SumAsync(balance => (decimal?)balance.Quantity, cancellationToken) ?? 0m;
        bool canViewAssets = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId, PermissionCodes.Assets.View, cancellationToken);
        bool canViewCustodies = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId, PermissionCodes.Custodies.View, cancellationToken);
        bool canViewDocuments = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.View, cancellationToken);
        bool canViewCounts = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId, PermissionCodes.InventoryCounts.View, cancellationToken);

        int? assetCount = canViewAssets
            ? await context.Assets.AsNoTracking().CountAsync(
                asset => asset.WarehouseId != null && warehouses.Contains(asset.WarehouseId.Value), cancellationToken)
            : null;
        int? activeCustodyCount = canViewCustodies
            ? await context.Custodies.AsNoTracking()
                .Where(custody => custody.Status == CustodyStatus.Active)
                .Join(context.Assets.AsNoTracking(), custody => custody.AssetId, asset => asset.Id, (_, asset) => asset)
                .CountAsync(
                    asset => asset.WarehouseId != null && warehouses.Contains(asset.WarehouseId.Value),
                    cancellationToken)
            : null;
        int? openDocumentCount = canViewDocuments
            ? await context.WarehouseDocuments.AsNoTracking().CountAsync(
                document => warehouses.Contains(document.WarehouseId) &&
                            (document.DocumentStatus == DocumentStatus.Draft ||
                             document.DocumentStatus == DocumentStatus.Submitted), cancellationToken)
            : null;
        int? activeCountCount = canViewCounts
            ? await context.InventoryCounts.AsNoTracking().CountAsync(
                count => warehouses.Contains(count.WarehouseId) &&
                         (count.Status == InventoryCountStatus.Planned ||
                          count.Status == InventoryCountStatus.InProgress ||
                          count.Status == InventoryCountStatus.Completed), cancellationToken)
            : null;

        return new DashboardReportResponse(
            warehouseCount,
            stockedMaterialCount,
            totalOnHand,
            assetCount,
            activeCustodyCount,
            openDocumentCount,
            activeCountCount,
            dateTimeProvider.UtcNow);
    }
}
