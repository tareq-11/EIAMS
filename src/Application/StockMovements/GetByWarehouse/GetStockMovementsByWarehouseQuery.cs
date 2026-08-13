using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.StockMovements.GetByWarehouse;

public sealed record GetStockMovementsByWarehouseQuery(Guid WarehouseId, int Page, int PageSize)
    : IQuery<PagedResult<StockMovementResponse>>;
