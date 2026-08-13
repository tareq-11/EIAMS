using SharedKernel;

namespace Domain.Assets;

/// <summary>
/// One individually numbered asset created by Receiving or Opening posting. Current state is
/// deliberately not stored here; M6 derives it from custody and movement history (D-AST-02).
/// </summary>
public sealed class Asset : Entity
{
    private Asset() { }

    public Guid MaterialId { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public Guid? ReceiptLineId { get; private set; }
    public string AssetNumber { get; private set; }
    public string? SerialNumber { get; private set; }
    public DateOnly AcquisitionDate { get; private set; }
    public DateOnly? WarrantyExpiry { get; private set; }
    public int RowVersion { get; private set; }

    public static Result<Asset> CreateReceived(
        Guid id,
        Guid materialId,
        Guid warehouseId,
        Guid receiptLineId,
        string assetNumber,
        DateOnly acquisitionDate,
        string? serialNumber = null,
        DateOnly? warrantyExpiry = null)
    {
        string normalizedAssetNumber = assetNumber.Trim();
        string? normalizedSerialNumber = string.IsNullOrWhiteSpace(serialNumber)
            ? null
            : serialNumber.Trim();

        if (string.IsNullOrWhiteSpace(normalizedAssetNumber) || normalizedAssetNumber.Length > 100)
        {
            return Result.Failure<Asset>(AssetErrors.AssetNumberInvalid);
        }

        if (normalizedSerialNumber?.Length > 200)
        {
            return Result.Failure<Asset>(AssetErrors.SerialNumberTooLong);
        }

        if (warrantyExpiry < acquisitionDate)
        {
            return Result.Failure<Asset>(AssetErrors.WarrantyBeforeAcquisition);
        }

        var asset = new Asset
        {
            Id = id,
            MaterialId = materialId,
            WarehouseId = warehouseId,
            ReceiptLineId = receiptLineId,
            AssetNumber = normalizedAssetNumber,
            SerialNumber = normalizedSerialNumber,
            AcquisitionDate = acquisitionDate,
            WarrantyExpiry = warrantyExpiry,
            RowVersion = 1
        };

        asset.Raise(new AssetCreatedDomainEvent(asset.Id, materialId, warehouseId, receiptLineId));

        return asset;
    }
}
