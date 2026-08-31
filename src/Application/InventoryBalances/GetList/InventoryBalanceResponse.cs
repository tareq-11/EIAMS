namespace Application.InventoryBalances.GetList;

public sealed record InventoryBalanceResponse(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid MaterialId,
    string MaterialCode,
    string MaterialNameAr,
    decimal Quantity,
    DateTime LastUpdatedUtc,
    int RowVersion);
