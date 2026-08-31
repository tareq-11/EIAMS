namespace Application.Assets.GetList;

public sealed record AssetResponse(
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
    Guid? ActiveCustodyId,
    string? HolderType,
    Guid? HolderId,
    DateOnly AcquisitionDate,
    DateOnly? WarrantyExpiry,
    int RowVersion);
