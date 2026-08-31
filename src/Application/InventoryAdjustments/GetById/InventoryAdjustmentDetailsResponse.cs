namespace Application.InventoryAdjustments.GetById;

public sealed record InventoryAdjustmentDetailsResponse(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid? CountId,
    string AdjustmentKind,
    string Status,
    string Reason,
    string SystemReferenceNumber,
    string DocumentStatus,
    DateTime CreatedAtUtc,
    DateTime? PostedAtUtc,
    int RowVersion,
    IReadOnlyList<InventoryAdjustmentLineResponse> Lines);

public sealed record InventoryAdjustmentLineResponse(
    Guid LineId,
    Guid MaterialId,
    string MaterialCode,
    string MaterialNameAr,
    decimal Quantity,
    decimal Difference,
    string Reason,
    IReadOnlyList<AdjustmentAssetResponse> Assets);

public sealed record AdjustmentAssetResponse(Guid Id, string AssetNumber, string? SerialNumber);
