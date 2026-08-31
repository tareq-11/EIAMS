using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.StockMovements.GetList;

public sealed record GetStockMovementsQuery(
    Guid? WarehouseId,
    Guid? MaterialId,
    Guid? DocumentId,
    MovementType? MovementType,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<StockMovementResponse>>;
