namespace Application.Assets.GetCurrentStatus;

public sealed record AssetCurrentStatusResponse(
    Guid AssetId,
    Guid MaterialId,
    Guid? WarehouseId,
    string AssetNumber,
    string? SerialNumber,
    string CurrentStatus,
    Guid? ActiveCustodyId,
    string? HolderType,
    Guid? HolderId,
    string? CustodyKind,
    string? LatestMovementType,
    DateTime? LatestMovementAtUtc);
