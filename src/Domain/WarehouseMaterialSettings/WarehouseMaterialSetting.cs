using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseMaterialSettings;

/// <summary>
/// Min/max stock thresholds for a material in a warehouse. Domain rule: both quantities are
/// non-negative and MinQuantity &lt;= MaxQuantity (mirrored by database CHECK constraints).
/// </summary>
public sealed class WarehouseMaterialSetting : Entity, IAuditableEntity
{
    private WarehouseMaterialSetting() { }

    public Guid WarehouseId { get; private set; }
    public Guid MaterialId { get; private set; }
    public decimal MinQuantity { get; private set; }
    public decimal MaxQuantity { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<WarehouseMaterialSetting> Create(
        Guid id,
        Guid warehouseId,
        Guid materialId,
        decimal minQuantity,
        decimal maxQuantity)
    {
        if (minQuantity < 0 || maxQuantity < 0 || minQuantity > maxQuantity)
        {
            return Result.Failure<WarehouseMaterialSetting>(
                WarehouseMaterialSettingErrors.InvalidRange(minQuantity, maxQuantity));
        }

        var setting = new WarehouseMaterialSetting
        {
            Id = id,
            WarehouseId = warehouseId,
            MaterialId = materialId,
            MinQuantity = minQuantity,
            MaxQuantity = maxQuantity,
            Status = Status.Active
        };

        setting.Raise(new WarehouseMaterialSettingCreatedDomainEvent(setting.Id, warehouseId, materialId));

        return setting;
    }

    public Result UpdateThresholds(decimal minQuantity, decimal maxQuantity)
    {
        if (minQuantity < 0 || maxQuantity < 0 || minQuantity > maxQuantity)
        {
            return Result.Failure(WarehouseMaterialSettingErrors.InvalidRange(minQuantity, maxQuantity));
        }

        MinQuantity = minQuantity;
        MaxQuantity = maxQuantity;

        Raise(new WarehouseMaterialSettingUpdatedDomainEvent(Id));

        return Result.Success();
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;

        Raise(new WarehouseMaterialSettingStatusChangedDomainEvent(Id, status));
    }
}
