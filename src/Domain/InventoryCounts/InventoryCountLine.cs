using SharedKernel;

namespace Domain.InventoryCounts;

public sealed class InventoryCountLine : Entity, IAuditableEntity
{
    private const decimal MaximumQuantity = 999_999_999_999_999.999m;
    private InventoryCountLine() { }

    public Guid CountId { get; private set; }
    public Guid MaterialId { get; private set; }
    public Guid? AssetId { get; private set; }
    public decimal SnapshotQuantity { get; private set; }
    public decimal? ActualQuantity { get; private set; }
    public decimal? Difference { get; private set; }
    public string? VarianceReason { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<InventoryCountLine> Create(
        Guid id,
        Guid countId,
        Guid materialId,
        Guid? assetId,
        decimal snapshotQuantity)
    {
        if (id == Guid.Empty || countId == Guid.Empty || materialId == Guid.Empty || assetId == Guid.Empty)
        {
            return Result.Failure<InventoryCountLine>(InventoryCountLineErrors.IdentityRequired);
        }

        if (!IsValidQuantity(snapshotQuantity))
        {
            return Result.Failure<InventoryCountLine>(InventoryCountLineErrors.QuantityInvalid);
        }

        if (assetId is not null && snapshotQuantity is not (0m or 1m))
        {
            return Result.Failure<InventoryCountLine>(InventoryCountLineErrors.AssetQuantityInvalid);
        }

        return new InventoryCountLine
        {
            Id = id,
            CountId = countId,
            MaterialId = materialId,
            AssetId = assetId,
            SnapshotQuantity = snapshotQuantity
        };
    }

    public Result RecordActual(decimal actualQuantity)
    {
        if (!IsValidQuantity(actualQuantity))
        {
            return Result.Failure(InventoryCountLineErrors.QuantityInvalid);
        }

        if (AssetId is not null && actualQuantity is not (0m or 1m))
        {
            return Result.Failure(InventoryCountLineErrors.AssetQuantityInvalid);
        }

        ActualQuantity = actualQuantity;
        Difference = actualQuantity - SnapshotQuantity;
        Raise(new InventoryCountActualRecordedDomainEvent(CountId, Id));
        return Result.Success();
    }

    public Result SetVarianceReason(string? reason)
    {
        string? normalized = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (normalized?.Length > 200)
        {
            return Result.Failure(InventoryCountLineErrors.VarianceReasonTooLong);
        }

        VarianceReason = normalized;
        Raise(new InventoryCountVarianceReasonUpdatedDomainEvent(CountId, Id));
        return Result.Success();
    }

    public bool HasRequiredVarianceReason() => Difference is null or 0m || !string.IsNullOrWhiteSpace(VarianceReason);

    private static bool IsValidQuantity(decimal quantity) =>
        quantity >= 0 && quantity <= MaximumQuantity && decimal.Round(quantity, 3) == quantity;
}
