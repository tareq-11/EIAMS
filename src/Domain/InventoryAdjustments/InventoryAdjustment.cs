using Domain.Common;
using SharedKernel;

namespace Domain.InventoryAdjustments;

public sealed class InventoryAdjustment : Entity, IAuditableEntity
{
    private InventoryAdjustment() { }

    public Guid? CountId { get; private set; }
    public AdjustmentKind AdjustmentKind { get; private set; }
    public InventoryAdjustmentStatus Status { get; private set; }
    public string Reason { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<InventoryAdjustment> Create(
        Guid documentId,
        Guid? countId,
        AdjustmentKind adjustmentKind,
        string reason)
    {
        if (documentId == Guid.Empty || countId == Guid.Empty)
        {
            return Result.Failure<InventoryAdjustment>(InventoryAdjustmentErrors.IdentityRequired);
        }

        if (!Enum.IsDefined(adjustmentKind))
        {
            return Result.Failure<InventoryAdjustment>(InventoryAdjustmentErrors.KindInvalid);
        }

        string normalized = reason?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return Result.Failure<InventoryAdjustment>(InventoryAdjustmentErrors.ReasonRequired);
        }

        if (normalized.Length > 500)
        {
            return Result.Failure<InventoryAdjustment>(InventoryAdjustmentErrors.ReasonTooLong);
        }

        var adjustment = new InventoryAdjustment
        {
            Id = documentId,
            CountId = countId,
            AdjustmentKind = adjustmentKind,
            Status = InventoryAdjustmentStatus.Draft,
            Reason = normalized
        };
        adjustment.Raise(new InventoryAdjustmentCreatedDomainEvent(documentId, countId, adjustmentKind));
        return adjustment;
    }

    public Result MarkPosted()
    {
        if (Status != InventoryAdjustmentStatus.Draft)
        {
            return Result.Failure(InventoryAdjustmentErrors.InvalidTransition(Id));
        }

        Status = InventoryAdjustmentStatus.Posted;
        Raise(new InventoryAdjustmentPostedDomainEvent(Id));
        return Result.Success();
    }

    public Result MarkReversed()
    {
        if (AdjustmentKind == AdjustmentKind.Disposal)
        {
            return Result.Failure(InventoryAdjustmentErrors.DisposalReversalNotAllowed(Id));
        }

        if (Status != InventoryAdjustmentStatus.Posted)
        {
            return Result.Failure(InventoryAdjustmentErrors.InvalidTransition(Id));
        }

        Status = InventoryAdjustmentStatus.Reversed;
        Raise(new InventoryAdjustmentReversedDomainEvent(Id));
        return Result.Success();
    }
}
