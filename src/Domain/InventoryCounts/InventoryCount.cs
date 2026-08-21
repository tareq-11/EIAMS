using Domain.Common;
using SharedKernel;

namespace Domain.InventoryCounts;

public sealed class InventoryCount : Entity, IAuditableEntity
{
    private InventoryCount() { }

    public Guid WarehouseId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public InventoryCountType CountType { get; private set; }
    public InventoryCountScopeType ScopeType { get; private set; }
    public Guid? ScopeMaterialDomainId { get; private set; }
    public FreezePolicy FreezePolicy { get; private set; }
    public InventoryCountStatus Status { get; private set; }
    public DateTime PlannedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public int RowVersion { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<InventoryCount> Plan(
        Guid id,
        Guid warehouseId,
        Guid createdByUserId,
        InventoryCountType countType,
        InventoryCountScopeType scopeType,
        Guid? scopeMaterialDomainId,
        FreezePolicy freezePolicy,
        DateTime plannedAtUtc)
    {
        if (id == Guid.Empty || warehouseId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            return Result.Failure<InventoryCount>(InventoryCountErrors.IdentityRequired);
        }

        if (!Enum.IsDefined(countType))
        {
            return Result.Failure<InventoryCount>(InventoryCountErrors.InvalidType);
        }

        if (!Enum.IsDefined(scopeType))
        {
            return Result.Failure<InventoryCount>(InventoryCountErrors.InvalidScope);
        }

        if (!Enum.IsDefined(freezePolicy))
        {
            return Result.Failure<InventoryCount>(InventoryCountErrors.InvalidFreezePolicy);
        }

        bool validScopeReference = scopeType == InventoryCountScopeType.MaterialDomain
            ? scopeMaterialDomainId is not null && scopeMaterialDomainId != Guid.Empty
            : scopeMaterialDomainId is null;

        if (!validScopeReference)
        {
            return Result.Failure<InventoryCount>(InventoryCountErrors.ScopeReferenceInvalid);
        }

        var count = new InventoryCount
        {
            Id = id,
            WarehouseId = warehouseId,
            CreatedByUserId = createdByUserId,
            CountType = countType,
            ScopeType = scopeType,
            ScopeMaterialDomainId = scopeMaterialDomainId,
            FreezePolicy = freezePolicy,
            Status = InventoryCountStatus.Planned,
            PlannedAtUtc = plannedAtUtc,
            RowVersion = 1
        };

        count.Raise(new InventoryCountPlannedDomainEvent(id, warehouseId));
        return count;
    }

    public Result Start(DateTime atUtc) => Transition(
        InventoryCountStatus.Planned,
        InventoryCountStatus.InProgress,
        atUtc,
        PlannedAtUtc,
        () => StartedAtUtc = atUtc,
        new InventoryCountStartedDomainEvent(Id, WarehouseId));

    public Result Complete(DateTime atUtc) => Transition(
        InventoryCountStatus.InProgress,
        InventoryCountStatus.Completed,
        atUtc,
        StartedAtUtc,
        () => CompletedAtUtc = atUtc,
        new InventoryCountCompletedDomainEvent(Id));

    public Result Close(DateTime atUtc) => Transition(
        InventoryCountStatus.Completed,
        InventoryCountStatus.Closed,
        atUtc,
        CompletedAtUtc,
        () => ClosedAtUtc = atUtc,
        new InventoryCountClosedDomainEvent(Id));

    public Result RegisterLineMutation()
    {
        if (Status is not (InventoryCountStatus.InProgress or InventoryCountStatus.Completed))
        {
            return Result.Failure(InventoryCountErrors.InvalidTransition(Id, Status, Status));
        }

        RowVersion++;
        return Result.Success();
    }

    private Result Transition(
        InventoryCountStatus expected,
        InventoryCountStatus target,
        DateTime atUtc,
        DateTime? earliestUtc,
        Action applyTimestamp,
        IDomainEvent domainEvent)
    {
        if (Status != expected)
        {
            return Result.Failure(InventoryCountErrors.InvalidTransition(Id, Status, target));
        }

        if (earliestUtc is null || atUtc < earliestUtc.Value)
        {
            return Result.Failure(InventoryCountErrors.TimestampInvalid);
        }

        applyTimestamp();
        Status = target;
        RowVersion++;
        Raise(domainEvent);
        return Result.Success();
    }
}
