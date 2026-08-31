using Domain.Common;
using SharedKernel;

namespace Domain.DurableCustodyAllocations;

/// <summary>
/// Represents quantity-tracked Durable responsibility/custody.
/// Supports partial returns and transfer of custody without mutating historical Issue document lines.
/// Invariant: IssuedQuantity = ActiveQuantity + ReturnedQuantity, IssuedQuantity > 0, ActiveQuantity >= 0, ReturnedQuantity >= 0.
/// </summary>
public sealed class DurableCustodyAllocation : Entity, IAuditableEntity
{
    private DurableCustodyAllocation() { }

    public Guid MaterialId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public PartyType HolderType { get; private set; }
    public Guid HolderId { get; private set; }
    public CustodyKind CustodyKind { get; private set; }
    public Guid IssueDocumentId { get; private set; }
    public decimal IssuedQuantity { get; private set; }
    public decimal ActiveQuantity { get; private set; }
    public decimal ReturnedQuantity { get; private set; }
    public DurableCustodyAllocationStatus Status { get; private set; }
    public DateTime FromUtc { get; private set; }
    public int RowVersion { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<DurableCustodyAllocation> Open(
        Guid id,
        Guid materialId,
        Guid warehouseId,
        PartyType holderType,
        Guid holderId,
        CustodyKind custodyKind,
        Guid issueDocumentId,
        decimal issuedQuantity,
        DateTime fromUtc)
    {
        Result validation = ValidateOpen(id, materialId, warehouseId, holderType, holderId, custodyKind, issueDocumentId, issuedQuantity);
        if (validation.IsFailure)
        {
            return Result.Failure<DurableCustodyAllocation>(validation.Error);
        }

        var allocation = new DurableCustodyAllocation
        {
            Id = id,
            MaterialId = materialId,
            WarehouseId = warehouseId,
            HolderType = holderType,
            HolderId = holderId,
            CustodyKind = custodyKind,
            IssueDocumentId = issueDocumentId,
            IssuedQuantity = issuedQuantity,
            ActiveQuantity = issuedQuantity,
            ReturnedQuantity = 0m,
            Status = DurableCustodyAllocationStatus.Active,
            FromUtc = fromUtc,
            RowVersion = 1
        };

        allocation.Raise(new DurableCustodyAllocationOpenedDomainEvent(
            id,
            materialId,
            holderType,
            holderId,
            custodyKind,
            issuedQuantity,
            issueDocumentId));

        return allocation;
    }

    public Result RecordReturn(decimal returnQuantity, Guid returnDocumentId, DateTime atUtc)
    {
        if (Status != DurableCustodyAllocationStatus.Active || ActiveQuantity <= 0)
        {
            return Result.Failure(DurableCustodyAllocationErrors.NotActive);
        }

        if (returnQuantity <= 0)
        {
            return Result.Failure(DurableCustodyAllocationErrors.QuantityMustBePositive);
        }

        if (returnDocumentId == Guid.Empty)
        {
            return Result.Failure(DurableCustodyAllocationErrors.ReturnDocumentRequired);
        }

        if (returnQuantity > ActiveQuantity)
        {
            return Result.Failure(DurableCustodyAllocationErrors.ReturnQuantityExceedsActive(returnQuantity, ActiveQuantity));
        }

        ActiveQuantity -= returnQuantity;
        ReturnedQuantity += returnQuantity;

        if (ActiveQuantity == 0)
        {
            Status = DurableCustodyAllocationStatus.FullyReturned;
        }

        RowVersion++;

        Raise(new DurableCustodyAllocationReturnedDomainEvent(
            Id,
            MaterialId,
            returnQuantity,
            ActiveQuantity,
            returnDocumentId,
            atUtc));

        return Result.Success();
    }

    public Result Transfer(
        PartyType newHolderType,
        Guid newHolderId,
        CustodyKind newCustodyKind,
        DateTime atUtc)
    {
        if (Status != DurableCustodyAllocationStatus.Active || ActiveQuantity <= 0)
        {
            return Result.Failure(DurableCustodyAllocationErrors.NotActive);
        }

        if (newHolderId == Guid.Empty)
        {
            return Result.Failure(DurableCustodyAllocationErrors.IdentityRequired);
        }

        if (!Enum.IsDefined(newHolderType))
        {
            return Result.Failure(DurableCustodyAllocationErrors.HolderTypeInvalid);
        }

        if (!Enum.IsDefined(newCustodyKind))
        {
            return Result.Failure(DurableCustodyAllocationErrors.CustodyKindInvalid);
        }

        if (newCustodyKind == CustodyKind.Personal && newHolderType != PartyType.Employee)
        {
            return Result.Failure(DurableCustodyAllocationErrors.PersonalRequiresEmployee);
        }

        if (newCustodyKind == CustodyKind.Operational && newHolderType == PartyType.Employee)
        {
            return Result.Failure(DurableCustodyAllocationErrors.OperationalRequiresNonEmployee);
        }

        HolderType = newHolderType;
        HolderId = newHolderId;
        CustodyKind = newCustodyKind;
        RowVersion++;

        Raise(new DurableCustodyAllocationTransferredDomainEvent(
            Id,
            newHolderType,
            newHolderId,
            newCustodyKind,
            atUtc));

        return Result.Success();
    }

    private static Result ValidateOpen(
        Guid id,
        Guid materialId,
        Guid warehouseId,
        PartyType holderType,
        Guid holderId,
        CustodyKind custodyKind,
        Guid issueDocumentId,
        decimal issuedQuantity)
    {
        if (id == Guid.Empty || materialId == Guid.Empty || warehouseId == Guid.Empty ||
            holderId == Guid.Empty || issueDocumentId == Guid.Empty)
        {
            return Result.Failure(DurableCustodyAllocationErrors.IdentityRequired);
        }

        if (issuedQuantity <= 0)
        {
            return Result.Failure(DurableCustodyAllocationErrors.QuantityMustBePositive);
        }

        if (!Enum.IsDefined(holderType))
        {
            return Result.Failure(DurableCustodyAllocationErrors.HolderTypeInvalid);
        }

        if (!Enum.IsDefined(custodyKind))
        {
            return Result.Failure(DurableCustodyAllocationErrors.CustodyKindInvalid);
        }

        if (custodyKind == CustodyKind.Personal && holderType != PartyType.Employee)
        {
            return Result.Failure(DurableCustodyAllocationErrors.PersonalRequiresEmployee);
        }

        if (custodyKind == CustodyKind.Operational && holderType == PartyType.Employee)
        {
            return Result.Failure(DurableCustodyAllocationErrors.OperationalRequiresNonEmployee);
        }

        return Result.Success();
    }
}
