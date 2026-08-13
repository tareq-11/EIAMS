using Domain.Common;

namespace Application.Abstractions.Assets;

/// <summary>
/// Defines stock movement types that conservatively indicate operational consumption of an asset
/// before per-asset movement and custody history is introduced in M6.
/// </summary>
public static class AssetDownstreamUsageRules
{
    public static IReadOnlyCollection<MovementType> OutboundMovementTypes { get; } =
    [
        MovementType.Issue,
        MovementType.TransferOut,
        MovementType.AdjustmentOut
    ];
}
