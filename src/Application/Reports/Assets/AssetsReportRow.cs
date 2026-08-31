namespace Application.Reports.Assets;

public sealed record AssetsReportRow(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    string CurrentStatus,
    int AssetCount);
