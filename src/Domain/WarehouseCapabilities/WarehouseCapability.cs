using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseCapabilities;

/// <summary>
/// A material domain a warehouse is allowed to handle (D-CAP-01). There is at most one capability
/// row per (WarehouseId, MaterialDomainId) pair - Grant reactivates an existing Inactive row rather
/// than creating a duplicate.
/// </summary>
public sealed class WarehouseCapability : Entity, IAuditableEntity
{
    private WarehouseCapability() { }

    public Guid WarehouseId { get; private set; }
    public Guid MaterialDomainId { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static WarehouseCapability Create(Guid id, Guid warehouseId, Guid materialDomainId)
    {
        var capability = new WarehouseCapability
        {
            Id = id,
            WarehouseId = warehouseId,
            MaterialDomainId = materialDomainId,
            Status = Status.Active
        };

        capability.Raise(new WarehouseCapabilityGrantedDomainEvent(capability.Id, warehouseId, materialDomainId));

        return capability;
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;

        Raise(new WarehouseCapabilityStatusChangedDomainEvent(Id, WarehouseId, MaterialDomainId, status));
    }
}
