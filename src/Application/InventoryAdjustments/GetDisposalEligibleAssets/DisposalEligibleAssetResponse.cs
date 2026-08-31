namespace Application.InventoryAdjustments.GetDisposalEligibleAssets;

public sealed record DisposalEligibleAssetResponse(
    Guid Id,
    string AssetNumber,
    string? SerialNumber,
    Guid MaterialId,
    string MaterialCode,
    string MaterialNameAr,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    string CurrentStatus,
    int RowVersion);
