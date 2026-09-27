using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.InventoryBalances;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryBalances.GetLowStock;

internal sealed class GetLowStockInventoryBalancesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetLowStockInventoryBalancesQuery, PagedResult<LowStockInventoryBalanceResponse>>
{
    public async Task<Result<PagedResult<LowStockInventoryBalanceResponse>>> Handle(
        GetLowStockInventoryBalancesQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId,
            PermissionCodes.Inventory.View,
            cancellationToken);

        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<LowStockInventoryBalanceResponse>>(InventoryBalanceErrors.Forbidden);
        }

        Guid[] allowedWarehouseIds = access.WarehouseIds.ToArray();
        IQueryable<LowStockInventoryBalanceResponse> source =
            from setting in context.WarehouseMaterialSettings.AsNoTracking()
            where setting.Status == Status.Active
            join warehouse in context.Warehouses.AsNoTracking() on setting.WarehouseId equals warehouse.Id
            join material in context.Materials.AsNoTracking() on setting.MaterialId equals material.Id
            join balance in context.InventoryBalances.AsNoTracking()
                on new { setting.WarehouseId, setting.MaterialId }
                equals new { balance.WarehouseId, balance.MaterialId } into balances
            from balance in balances.DefaultIfEmpty()
            where access.HasEnterpriseAccess || allowedWarehouseIds.Contains(setting.WarehouseId)
            where query.WarehouseId == null || setting.WarehouseId == query.WarehouseId
            where (balance == null ? 0m : balance.Quantity) < setting.MinQuantity
            orderby warehouse.Code, material.Code, setting.Id
            select new LowStockInventoryBalanceResponse(
                setting.Id,
                setting.WarehouseId,
                warehouse.Code,
                warehouse.Name,
                setting.MaterialId,
                material.Code,
                material.NameAr,
                balance == null ? 0m : balance.Quantity,
                setting.MinQuantity,
                setting.MaxQuantity,
                balance == null ? null : balance.LastUpdatedUtc);

        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<LowStockInventoryBalanceResponse> items = await source
            .Skip(offset)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LowStockInventoryBalanceResponse>(items, query.Page, query.PageSize, totalItems);
    }
}
