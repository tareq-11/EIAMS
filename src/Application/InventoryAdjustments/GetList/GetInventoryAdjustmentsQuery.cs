using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.InventoryAdjustments.GetList;

public sealed record GetInventoryAdjustmentsQuery(
    Guid? WarehouseId,
    AdjustmentKind? Kind,
    InventoryAdjustmentStatus? Status,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<InventoryAdjustmentResponse>>;
