using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.StockMovements.GetByDocument;

public sealed record GetStockMovementsByDocumentQuery(Guid DocumentId, int Page, int PageSize)
    : IQuery<PagedResult<StockMovementResponse>>;
