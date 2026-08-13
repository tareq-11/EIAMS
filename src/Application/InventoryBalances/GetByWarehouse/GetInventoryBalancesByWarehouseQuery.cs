using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.InventoryBalances.GetByWarehouse;

public sealed record GetInventoryBalancesByWarehouseQuery(Guid WarehouseId, int Page, int PageSize)
    : IQuery<PagedResult<InventoryBalanceResponse>>;
