using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseCapabilityOperations;

/// <summary>
/// One allowed operation (Receiving/Issue/Transfer/Count/Return) for a WarehouseCapability
/// (D-CAP-01). Unlike RolePermission, the PRD gives this table its own surrogate key (cap_op_id),
/// so - unlike RolePermission - it is a full <see cref="Entity"/> with audit columns rather than a
/// bare composite-key join row.
/// </summary>
public sealed class WarehouseCapabilityOperation : Entity, IAuditableEntity
{
    private WarehouseCapabilityOperation() { }

    public Guid CapabilityId { get; private set; }
    public OperationType OperationType { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static WarehouseCapabilityOperation Create(Guid id, Guid capabilityId, OperationType operationType)
    {
        var operation = new WarehouseCapabilityOperation
        {
            Id = id,
            CapabilityId = capabilityId,
            OperationType = operationType
        };

        operation.Raise(new WarehouseCapabilityOperationAddedDomainEvent(operation.Id, capabilityId, operationType));

        return operation;
    }

    public void MarkAsRemoved()
    {
        Raise(new WarehouseCapabilityOperationRemovedDomainEvent(Id, CapabilityId, OperationType));
    }
}
