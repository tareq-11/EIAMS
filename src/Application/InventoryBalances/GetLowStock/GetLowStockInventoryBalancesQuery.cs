using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.InventoryBalances.GetLowStock;

public sealed record GetLowStockInventoryBalancesQuery(
    Guid? WarehouseId,
    int Page,
    int PageSize) : IQuery<PagedResult<LowStockInventoryBalanceResponse>>;
