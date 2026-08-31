namespace Application.InventoryAdjustments.GetList;

public sealed record InventoryAdjustmentResponse(
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
    int LineCount,
    decimal TotalDifference,
    DateTime CreatedAtUtc,
    DateTime? PostedAtUtc,
    int RowVersion);
