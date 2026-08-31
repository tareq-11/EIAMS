namespace Application.StockMovements.GetList;

public sealed record StockMovementResponse(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid MaterialId,
    string MaterialCode,
    string MaterialNameAr,
    Guid DocumentId,
    string DocumentReferenceNumber,
    Guid LineId,
    string MovementType,
    decimal QuantityDelta,
    DateTime PostedAtUtc,
    Guid PostedBy);
