using SharedKernel;

namespace Domain.Materials;

/// <summary>
/// Server-side classification matrix for decision 032. The kind/tracking pair is validated in one
/// place so the command handlers, the draft line derivation and the catalog readers cannot drift:
/// a consumable is always quantity tracked, an asset is always serial tracked, and a durable may be
/// tracked either way. Nothing here is client-writable, and <c>RequiresAssetNumber</c> is derived
/// from the kind instead of being accepted from a request.
/// </summary>
public static class MaterialClassification
{
    public static Result Validate(MaterialKind materialKind, TrackingType trackingType)
    {
        if (materialKind == MaterialKind.Consumable && trackingType != TrackingType.Quantity)
        {
            return Result.Failure(MaterialErrors.ConsumableMustBeQuantityTracked);
        }

        if (materialKind == MaterialKind.Asset && trackingType != TrackingType.Serial)
        {
            return Result.Failure(MaterialErrors.AssetMustBeSerialTracked);
        }

        return Result.Success();
    }

    public static bool RequiresAssetNumber(MaterialKind materialKind) => materialKind == MaterialKind.Asset;
}
