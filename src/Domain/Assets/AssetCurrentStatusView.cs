using Domain.Common;

namespace Domain.Assets;

/// <summary>Read-only projection of PostgreSQL view <c>v_asset_current_status</c>.</summary>
public sealed class AssetCurrentStatusView
{
    public Guid AssetId { get; init; }
    public Guid MaterialId { get; init; }
    public Guid? WarehouseId { get; init; }
    public string AssetNumber { get; init; }
    public string? SerialNumber { get; init; }
    public AssetCurrentStatus CurrentStatus { get; init; }
    public Guid? ActiveCustodyId { get; init; }
    public PartyType? HolderType { get; init; }
    public Guid? HolderId { get; init; }
    public CustodyKind? CustodyKind { get; init; }
    public AssetMovementType? LatestMovementType { get; init; }
    public DateTime? LatestMovementAtUtc { get; init; }
}
