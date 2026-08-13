using SharedKernel;

namespace Domain.Assets;

public static class AssetErrors
{
    public static readonly Error AssetNumberInvalid = Error.Problem(
        "Assets.AssetNumberInvalid",
        "Asset number is required and must not exceed 100 characters.");

    public static readonly Error SerialNumberTooLong = Error.Problem(
        "Assets.SerialNumberTooLong",
        "Serial number must not exceed 200 characters.");

    public static readonly Error WarrantyBeforeAcquisition = Error.Problem(
        "Assets.WarrantyBeforeAcquisition",
        "Warranty expiry cannot be earlier than the acquisition date.");

    public static Error DuplicateAssetNumber(string assetNumber) => Error.Conflict(
        "Assets.DuplicateAssetNumber",
        $"The asset number '{assetNumber}' already exists.",
        new { asset_number = assetNumber });

    public static Error ReversalBlocked(Guid documentId) => Error.Conflict(
        "Assets.ReversalBlocked",
        "Assets created by this document already have downstream usage and cannot be removed.",
        new { document_id = documentId });
}
