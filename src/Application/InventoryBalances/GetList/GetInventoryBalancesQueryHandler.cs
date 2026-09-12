using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Abstractions.Searching;
using Domain.Common;
using Domain.InventoryBalances;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryBalances.GetList;

internal sealed class GetInventoryBalancesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetInventoryBalancesQuery, PagedResult<InventoryBalanceResponse>>
{
    public async Task<Result<PagedResult<InventoryBalanceResponse>>> Handle(
        GetInventoryBalancesQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId,
            PermissionCodes.Inventory.View,
            cancellationToken);

        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<InventoryBalanceResponse>>(InventoryBalanceErrors.Forbidden);
        }

        Guid[] allowedWarehouseIds = access.WarehouseIds.ToArray();
        string? search = SqlLikePattern.CreateContains(query.Search, normalizeToUpper: true);

        IQueryable<InventoryBalanceResponse> source =
            from balance in context.InventoryBalances.AsNoTracking()
            join warehouse in context.Warehouses.AsNoTracking() on balance.WarehouseId equals warehouse.Id
            join material in context.Materials.AsNoTracking() on balance.MaterialId equals material.Id
            where access.HasEnterpriseAccess || allowedWarehouseIds.Contains(balance.WarehouseId)
            where query.WarehouseId == null || balance.WarehouseId == query.WarehouseId
            where query.MaterialId == null || balance.MaterialId == query.MaterialId
            where search == null ||
#pragma warning disable CA1304, CA1311 // Translated by EF Core to the database UPPER function.
                  EF.Functions.Like(warehouse.Code.ToUpper(), search, SqlLikePattern.EscapeCharacter) ||
                  EF.Functions.Like(warehouse.Name.ToUpper(), search, SqlLikePattern.EscapeCharacter) ||
                  EF.Functions.Like(material.Code.ToUpper(), search, SqlLikePattern.EscapeCharacter) ||
                  EF.Functions.Like(material.NameAr.ToUpper(), search, SqlLikePattern.EscapeCharacter)
#pragma warning restore CA1304, CA1311
            orderby warehouse.Code, material.Code, balance.Id
            select new InventoryBalanceResponse(
                balance.Id,
                balance.WarehouseId,
                warehouse.Code,
                warehouse.Name,
                balance.MaterialId,
                material.Code,
                material.NameAr,
                balance.Quantity,
                balance.LastUpdatedUtc,
                balance.RowVersion);

        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<InventoryBalanceResponse> items = await source
            .Skip(offset)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryBalanceResponse>(items, query.Page, query.PageSize, totalItems);
    }
}
