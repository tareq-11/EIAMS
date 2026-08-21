using Domain.Common;

namespace Application.InventoryCounts.GetById;

public sealed record InventoryCountSummaryResponse(
    int TotalLines,
    int CountedLines,
    int VarianceLines,
    decimal TotalAbsoluteDifference);

public sealed record InventoryCountDetailsResponse(
    Guid Id, Guid WarehouseId, InventoryCountType CountType, InventoryCountScopeType ScopeType,
    Guid? MaterialDomainId, FreezePolicy FreezePolicy, InventoryCountStatus Status, int RowVersion,
    DateTime PlannedAtUtc, DateTime? StartedAtUtc, DateTime? CompletedAtUtc, DateTime? ClosedAtUtc,
    InventoryCountSummaryResponse Summary);
