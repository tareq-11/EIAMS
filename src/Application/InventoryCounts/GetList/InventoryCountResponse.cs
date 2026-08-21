using Domain.Common;

namespace Application.InventoryCounts.GetList;

public sealed record InventoryCountResponse(
    Guid Id,
    Guid WarehouseId,
    InventoryCountType CountType,
    InventoryCountScopeType ScopeType,
    FreezePolicy FreezePolicy,
    InventoryCountStatus Status,
    int RowVersion,
    DateTime PlannedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? ClosedAtUtc);
