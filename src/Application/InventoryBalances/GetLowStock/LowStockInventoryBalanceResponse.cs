namespace Application.InventoryBalances.GetLowStock;

public sealed record LowStockInventoryBalanceResponse(
    Guid SettingId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid MaterialId,
    string MaterialCode,
    string MaterialNameAr,
    decimal Quantity,
    decimal MinQuantity,
    decimal MaxQuantity,
    DateTime? LastUpdatedUtc);
