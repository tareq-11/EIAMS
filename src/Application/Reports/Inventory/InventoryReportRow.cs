namespace Application.Reports.Inventory;

public sealed record InventoryReportRow(
    Guid MaterialId,
    string MaterialCode,
    string MaterialNameAr,
    decimal TotalQuantity,
    int WarehouseCount,
    DateTime LastUpdatedUtc);
