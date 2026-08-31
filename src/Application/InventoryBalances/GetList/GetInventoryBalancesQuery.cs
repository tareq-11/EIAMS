using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.InventoryBalances.GetList;

public sealed record GetInventoryBalancesQuery(
    Guid? WarehouseId,
    Guid? MaterialId,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<InventoryBalanceResponse>>;
