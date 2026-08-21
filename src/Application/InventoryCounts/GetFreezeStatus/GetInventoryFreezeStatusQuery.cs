using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.InventoryCounts.GetFreezeStatus;

public sealed record GetInventoryFreezeStatusQuery(Guid WarehouseId)
    : IQuery<InventoryFreezeStatusResponse>;

public sealed record ActiveInventoryFreezeResponse(
    Guid CountId,
    FreezePolicy FreezePolicy);

public sealed record InventoryFreezeStatusResponse(
    Guid WarehouseId,
    bool IsPostingBlocked,
    bool HasSoftFreezeWarning,
    IReadOnlyList<ActiveInventoryFreezeResponse> ActiveCounts);
