using SharedKernel;

namespace Domain.InventoryAdjustments;

public static class DisposalErrors
{
    public static Error AssetAlreadyDisposed(Guid assetId) => Error.Conflict("Disposals.AssetAlreadyDisposed", "The asset is already disposed and its lifecycle is terminal.", new { asset_id = assetId });
    public static Error AssetStateChanged(Guid assetId) => Error.Conflict("Disposals.AssetStateChanged", "The asset state changed before disposal could be posted.", new { asset_id = assetId });
    public static Error UnsupportedState(Guid assetId) => Error.Problem("Disposals.UnsupportedState", "Only an in-stock or actively custodied asset can be disposed.", new { asset_id = assetId });
    public static Error AlreadyPending(Guid assetId) => Error.Conflict("Disposals.AlreadyPending", "The asset is already reserved by another active disposal document.", new { asset_id = assetId });
}
