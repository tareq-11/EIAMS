using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryAdjustments.GetList;

internal sealed class GetInventoryAdjustmentsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetInventoryAdjustmentsQuery, PagedResult<InventoryAdjustmentResponse>>
{
    public async Task<Result<PagedResult<InventoryAdjustmentResponse>>> Handle(
        GetInventoryAdjustmentsQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            cancellationToken);
        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<InventoryAdjustmentResponse>>(WarehouseDocumentErrors.Forbidden);
        }

        Guid[] warehouseIds = access.WarehouseIds.ToArray();
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().ToUpperInvariant();
        DateTime? fromDate = query.FromUtc?.UtcDateTime;
        DateTime? toDate = query.ToUtc?.UtcDateTime;

        IQueryable<InventoryAdjustmentResponse> source =
            from adjustment in context.InventoryAdjustments.AsNoTracking()
            join document in context.WarehouseDocuments.AsNoTracking() on adjustment.Id equals document.Id
            join warehouse in context.Warehouses.AsNoTracking() on document.WarehouseId equals warehouse.Id
            where access.HasEnterpriseAccess || warehouseIds.Contains(document.WarehouseId)
            where query.WarehouseId == null || document.WarehouseId == query.WarehouseId
            where query.Kind == null || adjustment.AdjustmentKind == query.Kind
            where query.Status == null || adjustment.Status == query.Status
            where fromDate == null || adjustment.CreatedAtUtc >= fromDate
            where toDate == null || adjustment.CreatedAtUtc < toDate
#pragma warning disable CA1304, CA1311 // Translated by EF Core into SQL UPPER.
            where search == null || EF.Functions.Like(adjustment.Reason.ToUpper(), $"%{search}%") ||
                  EF.Functions.Like(document.SystemReferenceNumber.ToUpper(), $"%{search}%")
#pragma warning restore CA1304, CA1311
            orderby adjustment.CreatedAtUtc descending, adjustment.Id descending
            select new InventoryAdjustmentResponse(
                adjustment.Id,
                document.WarehouseId,
                warehouse.Code,
                warehouse.Name,
                adjustment.CountId,
                adjustment.AdjustmentKind.ToString(),
                adjustment.Status.ToString(),
                adjustment.Reason,
                document.SystemReferenceNumber,
                document.DocumentStatus.ToString(),
                context.AdjustmentLines.Count(line => line.AdjustmentId == adjustment.Id),
                context.AdjustmentLines
                    .Where(line => line.AdjustmentId == adjustment.Id)
                    .Sum(line => (decimal?)line.Difference) ?? 0m,
                adjustment.CreatedAtUtc,
                document.PostedAtUtc,
                document.RowVersion);

        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<InventoryAdjustmentResponse> items = await source.Skip(offset).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<InventoryAdjustmentResponse>(items, query.Page, query.PageSize, totalItems);
    }
}
