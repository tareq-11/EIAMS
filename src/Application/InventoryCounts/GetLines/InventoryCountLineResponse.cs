namespace Application.InventoryCounts.GetLines;

public sealed record InventoryCountLineResponse(
    Guid Id,
    Guid MaterialId,
    Guid? AssetId,
    decimal SnapshotQuantity,
    decimal? ActualQuantity,
    decimal? Difference,
    string? VarianceReason);
