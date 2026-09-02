using Application.Abstractions.Audit;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Pagination;
using Domain.AuditLogs;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AuditLogs;

internal static class AuditLogQuerySupport
{
    public static bool AreGlobalFiltersValid(
        Guid? userId,
        string? entityType,
        Guid? entityId,
        string? action,
        string? fieldName,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? search,
        int page,
        int pageSize) =>
        userId != Guid.Empty &&
        entityId != Guid.Empty &&
        (entityType is null || KnownAuditEntityTypes.IsKnown(entityType)) &&
        AreFiltersValid(action, fromUtc, toUtc) &&
        (fieldName is null || IsCanonicalFieldName(fieldName)) &&
        (search is null || search.Trim().Length is > 0 and <= 200) &&
        page is >= PaginationDefaults.DefaultPage and <= PaginationDefaults.MaximumPage &&
        pageSize is >= 1 and <= PaginationDefaults.MaximumPageSize;

    public static bool AreFiltersValid(string? action, DateTimeOffset? fromUtc, DateTimeOffset? toUtc) =>
        (action is null || AuditActions.All.Contains(action)) &&
        (!fromUtc.HasValue || !toUtc.HasValue || fromUtc.Value < toUtc.Value);

    public static bool ArePaginationParametersValid(int page, int pageSize) =>
        page is >= PaginationDefaults.DefaultPage and <= PaginationDefaults.MaximumPage &&
        pageSize is >= 1 and <= PaginationDefaults.MaximumPageSize;

    public static IQueryable<AuditLog> ApplyFilters(
        IQueryable<AuditLog> query,
        string? action,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        if (action is not null)
        {
            query = query.Where(item => item.Action == action);
        }

        if (fromUtc.HasValue)
        {
            DateTime from = fromUtc.Value.UtcDateTime;
            query = query.Where(item => item.CreatedAtUtc >= from);
        }

        if (toUtc.HasValue)
        {
            DateTime to = toUtc.Value.UtcDateTime;
            query = query.Where(item => item.CreatedAtUtc < to);
        }

        return query;
    }

    public static async Task<Result<IQueryable<AuditLog>>> AuthorizeAndApplyScopeAsync(
        IQueryable<AuditLog> source,
        IApplicationDbContext context,
        IScopeAuthorizationService scopeAuthorizationService,
        Guid userId,
        CancellationToken cancellationToken)
    {
        bool hasPermission = await scopeAuthorizationService.HasPermissionAsync(
            userId,
            PermissionCodes.AuditLogs.View,
            cancellationToken);

        if (!hasPermission)
        {
            return Result.Failure<IQueryable<AuditLog>>(AuditLogErrors.Unauthorized);
        }

        UserAuthorizationAssignment? assignment = await scopeAuthorizationService.GetUserAssignmentAsync(
            userId,
            cancellationToken);

        if (assignment is null)
        {
            return Result.Failure<IQueryable<AuditLog>>(AuditLogErrors.NoScopeAssigned);
        }

        if (assignment.ScopeType == ScopeType.Enterprise)
        {
            return Result.Success(source);
        }

        SitePermissionScope siteScope = await scopeAuthorizationService.GetSitePermissionScopeAsync(
            userId,
            PermissionCodes.AuditLogs.View,
            cancellationToken);
        OrganizationalUnitPermissionScope organizationalUnitScope =
            await scopeAuthorizationService.GetOrganizationalUnitPermissionScopeAsync(
                userId,
                PermissionCodes.AuditLogs.View,
                cancellationToken);
        WarehousePermissionScope warehouseScope = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userId,
            PermissionCodes.AuditLogs.View,
            cancellationToken);

        Guid[] siteIds = assignment.ScopeType == ScopeType.Site
            ? siteScope.SiteIds.ToArray()
            : [];
        Guid[] organizationalUnitIds = organizationalUnitScope.OrganizationalUnitIds.ToArray();
        Guid[] warehouseIds = warehouseScope.WarehouseIds.ToArray();

        return Result.Success(ApplyNonEnterpriseScope(
            source,
            context,
            siteIds,
            organizationalUnitIds,
            warehouseIds));
    }

    private static IQueryable<AuditLog> ApplyNonEnterpriseScope(
        IQueryable<AuditLog> source,
        IApplicationDbContext context,
        Guid[] siteIds,
        Guid[] organizationalUnitIds,
        Guid[] warehouseIds) =>
        source.Where(log =>
            // Administrative hierarchy.
            log.EntityType == "Site" && siteIds.Contains(log.EntityId) ||
            log.EntityType == "OrganizationalUnit" && organizationalUnitIds.Contains(log.EntityId) ||
            log.EntityType == "Employee" && context.Employees.Any(employee =>
                employee.Id == log.EntityId && organizationalUnitIds.Contains(employee.OrgUnitId)) ||

            // Warehouse configuration and current inventory.
            log.EntityType == "Warehouse" && warehouseIds.Contains(log.EntityId) ||
            log.EntityType == "WarehouseCapability" && context.WarehouseCapabilities.Any(capability =>
                capability.Id == log.EntityId && warehouseIds.Contains(capability.WarehouseId)) ||
            log.EntityType == "WarehouseCapabilityOperation" &&
                context.WarehouseCapabilityOperations.Any(operation =>
                    operation.Id == log.EntityId && context.WarehouseCapabilities.Any(capability =>
                        capability.Id == operation.CapabilityId && warehouseIds.Contains(capability.WarehouseId))) ||
            log.EntityType == "WarehouseMaterialSetting" && context.WarehouseMaterialSettings.Any(setting =>
                setting.Id == log.EntityId && warehouseIds.Contains(setting.WarehouseId)) ||
            log.EntityType == "InventoryBalance" && context.InventoryBalances.Any(balance =>
                balance.Id == log.EntityId && warehouseIds.Contains(balance.WarehouseId)) ||

            // Warehouse documents and every detail/petal whose registry aggregate is the document.
            log.EntityType == "WarehouseDocument" && context.WarehouseDocuments.Any(document =>
                document.Id == log.EntityId && warehouseIds.Contains(document.WarehouseId)) ||
            log.AggregateType == "WarehouseDocument" && log.AggregateId.HasValue &&
                context.WarehouseDocuments.Any(document =>
                    document.Id == log.AggregateId.Value && warehouseIds.Contains(document.WarehouseId)) ||
            log.EntityType == "InventoryAdjustment" && context.WarehouseDocuments.Any(document =>
                document.Id == log.EntityId && warehouseIds.Contains(document.WarehouseId)) ||

            // Inventory counts and their child rows.
            log.EntityType == "InventoryCount" && context.InventoryCounts.Any(count =>
                count.Id == log.EntityId && warehouseIds.Contains(count.WarehouseId)) ||
            log.AggregateType == "InventoryCount" && log.AggregateId.HasValue &&
                context.InventoryCounts.Any(count =>
                    count.Id == log.AggregateId.Value && warehouseIds.Contains(count.WarehouseId)) ||

            // Assets and asset custody/history.
            log.EntityType == "Asset" && context.Assets.Any(asset =>
                asset.Id == log.EntityId && asset.WarehouseId.HasValue && warehouseIds.Contains(asset.WarehouseId.Value)) ||
            log.AggregateType == "Asset" && log.AggregateId.HasValue && context.Assets.Any(asset =>
                asset.Id == log.AggregateId.Value && asset.WarehouseId.HasValue && warehouseIds.Contains(asset.WarehouseId.Value)) ||
            log.EntityType == "Custody" && context.Custodies.Any(custody =>
                custody.Id == log.EntityId && context.Assets.Any(asset =>
                    asset.Id == custody.AssetId && asset.WarehouseId.HasValue && warehouseIds.Contains(asset.WarehouseId.Value))) ||
            log.AggregateType == "Custody" && log.AggregateId.HasValue && context.Custodies.Any(custody =>
                custody.Id == log.AggregateId.Value && context.Assets.Any(asset =>
                    asset.Id == custody.AssetId && asset.WarehouseId.HasValue && warehouseIds.Contains(asset.WarehouseId.Value))) ||

            // Durable responsibility/custody roots and history.
            log.EntityType == "TrackedMaterialUnit" && context.TrackedMaterialUnits.Any(unit =>
                unit.Id == log.EntityId && warehouseIds.Contains(unit.WarehouseId)) ||
            log.EntityType == "DurableCustodyAllocation" && context.DurableCustodyAllocations.Any(allocation =>
                allocation.Id == log.EntityId && warehouseIds.Contains(allocation.WarehouseId)) ||
            log.AggregateType == "DurableCustody" && log.AggregateId.HasValue &&
                (context.TrackedMaterialUnits.Any(unit =>
                     unit.Id == log.AggregateId.Value && warehouseIds.Contains(unit.WarehouseId)) ||
                 context.DurableCustodyAllocations.Any(allocation =>
                     allocation.Id == log.AggregateId.Value && warehouseIds.Contains(allocation.WarehouseId))));

    private static bool IsCanonicalFieldName(string fieldName)
    {
        if (fieldName.Length is < 1 or > AuditLogEntry.MaxFieldNameLength ||
            fieldName[0] is < 'a' or > 'z')
        {
            return false;
        }

        return fieldName.All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }

    public static async Task<PagedResult<AuditLogListItemResponse>> ToPagedResultAsync(
        IQueryable<AuditLog> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken,
        IAuditRedactionService? redactionService = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, PaginationDefaults.DefaultPage);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, PaginationDefaults.MaximumPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, PaginationDefaults.MaximumPageSize);

        int totalItems = await query.CountAsync(cancellationToken);
        int offset = checked((page - 1) * pageSize);

        List<AuditLog> rawItems = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        List<AuditLogListItemResponse> items = MapListItems(rawItems, redactionService);

        return new PagedResult<AuditLogListItemResponse>(items, page, pageSize, totalItems);
    }

    public static async Task<KeysetPage<AuditLogListItemResponse>> ToKeysetPageAsync(
        IQueryable<AuditLog> query,
        int pageSize,
        CancellationToken cancellationToken,
        IAuditRedactionService? redactionService = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, PaginationDefaults.MaximumPageSize);

        List<AuditLog> rawItems = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);
        bool hasMore = rawItems.Count > pageSize;
        if (hasMore)
        {
            rawItems.RemoveAt(rawItems.Count - 1);
        }

        List<AuditLogListItemResponse> items = MapListItems(rawItems, redactionService);
        AuditLog? lastItem = rawItems.LastOrDefault();

        return new KeysetPage<AuditLogListItemResponse>(
            items,
            pageSize,
            hasMore,
            hasMore ? lastItem?.CreatedAtUtc : null,
            hasMore ? lastItem?.Id : null);
    }

    private static List<AuditLogListItemResponse> MapListItems(
        IEnumerable<AuditLog> rawItems,
        IAuditRedactionService? redactionService) =>
        rawItems.Select(item => new AuditLogListItemResponse(
            item.Id,
            item.OperationId,
            item.RequestId,
            item.UserId,
            item.EntityType,
            item.EntityId,
            item.AggregateType,
            item.AggregateId,
            item.Action,
            item.CommandName,
            item.CreatedAtUtc,
            redactionService?.GetActionDisplayAr(item.Action),
            redactionService?.GetActionDisplayEn(item.Action),
            redactionService?.GetEntityTypeDisplayAr(item.EntityType),
            redactionService?.GetEntityTypeDisplayEn(item.EntityType))).ToList();
}
