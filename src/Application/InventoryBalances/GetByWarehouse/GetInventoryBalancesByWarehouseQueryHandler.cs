using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryBalances.GetByWarehouse;

internal sealed class GetInventoryBalancesByWarehouseQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetInventoryBalancesByWarehouseQuery, PagedResult<InventoryBalanceResponse>>
{
    public async Task<Result<PagedResult<InventoryBalanceResponse>>> Handle(
        GetInventoryBalancesByWarehouseQuery query,
        CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            query.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<PagedResult<InventoryBalanceResponse>>(WarehouseErrors.NotFound(query.WarehouseId));
        }

        if (!await context.Warehouses.AnyAsync(w => w.Id == query.WarehouseId, cancellationToken))
        {
            return Result.Failure<PagedResult<InventoryBalanceResponse>>(WarehouseErrors.NotFound(query.WarehouseId));
        }

        PagedResult<InventoryBalanceResponse> balances = await (
                from balance in context.InventoryBalances
                where balance.WarehouseId == query.WarehouseId
                join material in context.Materials on balance.MaterialId equals material.Id
                select new InventoryBalanceResponse
                {
                    Id = balance.Id,
                    WarehouseId = balance.WarehouseId,
                    MaterialId = balance.MaterialId,
                    MaterialCode = material.Code,
                    MaterialNameAr = material.NameAr,
                    Quantity = balance.Quantity,
                    LastUpdatedUtc = balance.LastUpdatedUtc,
                    RowVersion = balance.RowVersion
                })
            .OrderBy(b => b.MaterialCode)
            .ThenBy(b => b.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return balances;
    }
}
