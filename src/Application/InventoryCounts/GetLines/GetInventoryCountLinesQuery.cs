using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.InventoryCounts.GetLines;

public sealed record GetInventoryCountLinesQuery(
    Guid CountId,
    bool OnlyVariance,
    int Page,
    int PageSize) : IQuery<PagedResult<InventoryCountLineResponse>>;
