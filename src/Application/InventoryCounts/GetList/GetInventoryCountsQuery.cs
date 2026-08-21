using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.InventoryCounts.GetList;

public sealed record GetInventoryCountsQuery(
    Guid WarehouseId,
    InventoryCountStatus? Status,
    int Page,
    int PageSize) : IQuery<PagedResult<InventoryCountResponse>>;
