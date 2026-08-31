using Domain.Common;
using SharedKernel;

namespace Domain.TrackedMaterialUnits;

/// <summary>
/// Represents an individual serial-tracked Durable item issued under responsibility/custody.
/// Unlike Assets, durable units do not have internal asset numbers or depreciation schedules.
/// </summary>
public sealed class TrackedMaterialUnit : Entity, IAuditableEntity
{
    private TrackedMaterialUnit() { }

    public Guid MaterialId { get; private set; }
    public string SerialNumber { get; private set; }
    public Guid WarehouseId { get; private set; }
    public PartyType HolderType { get; private set; }
    public Guid HolderId { get; private set; }
    public CustodyKind CustodyKind { get; private set; }
    public Guid IssueDocumentId { get; private set; }
    public Guid? ReturnDocumentId { get; private set; }
    public TrackedMaterialUnitStatus Status { get; private set; }
    public DateTime FromUtc { get; private set; }
    public DateTime? ToUtc { get; private set; }
    public int RowVersion { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<TrackedMaterialUnit> Issue(
        Guid id,
        Guid materialId,
        string serialNumber,
        Guid warehouseId,
        PartyType holderType,
        Guid holderId,
        CustodyKind custodyKind,
        Guid issueDocumentId,
        DateTime fromUtc)
    {
        Result validation = ValidateIssue(id, materialId, serialNumber, warehouseId, holderType, holderId, custodyKind, issueDocumentId);
        if (validation.IsFailure)
        {
            return Result.Failure<TrackedMaterialUnit>(validation.Error);
        }

        var unit = new TrackedMaterialUnit
        {
            Id = id,
            MaterialId = materialId,
            SerialNumber = serialNumber.Trim(),
            WarehouseId = warehouseId,
            HolderType = holderType,
            HolderId = holderId,
            CustodyKind = custodyKind,
            IssueDocumentId = issueDocumentId,
            Status = TrackedMaterialUnitStatus.Issued,
            FromUtc = fromUtc,
            RowVersion = 1
        };

        unit.Raise(new TrackedMaterialUnitIssuedDomainEvent(
            id,
            materialId,
            unit.SerialNumber,
            holderType,
            holderId,
            custodyKind,
            issueDocumentId));

        return unit;
    }

    public Result Return(Guid returnDocumentId, DateTime atUtc)
    {
        if (Status != TrackedMaterialUnitStatus.Issued)
        {
            return Result.Failure(TrackedMaterialUnitErrors.NotActive);
        }

        if (returnDocumentId == Guid.Empty)
        {
            return Result.Failure(TrackedMaterialUnitErrors.ReturnDocumentRequired);
        }

        if (atUtc < FromUtc)
        {
            return Result.Failure(TrackedMaterialUnitErrors.CloseTimeInvalid);
        }

        ReturnDocumentId = returnDocumentId;
        ToUtc = atUtc;
        Status = TrackedMaterialUnitStatus.Returned;
        RowVersion++;

        Raise(new TrackedMaterialUnitReturnedDomainEvent(Id, MaterialId, returnDocumentId, atUtc));

        return Result.Success();
    }

    public Result Transfer(
        PartyType newHolderType,
        Guid newHolderId,
        CustodyKind newCustodyKind,
        DateTime atUtc)
    {
        if (Status != TrackedMaterialUnitStatus.Issued)
        {
            return Result.Failure(TrackedMaterialUnitErrors.NotActive);
        }

        if (newHolderId == Guid.Empty)
        {
            return Result.Failure(TrackedMaterialUnitErrors.IdentityRequired);
        }

        if (!Enum.IsDefined(newHolderType))
        {
            return Result.Failure(TrackedMaterialUnitErrors.HolderTypeInvalid);
        }

        if (!Enum.IsDefined(newCustodyKind))
        {
            return Result.Failure(TrackedMaterialUnitErrors.CustodyKindInvalid);
        }

        if (newCustodyKind == CustodyKind.Personal && newHolderType != PartyType.Employee)
        {
            return Result.Failure(TrackedMaterialUnitErrors.PersonalRequiresEmployee);
        }

        if (newCustodyKind == CustodyKind.Operational && newHolderType == PartyType.Employee)
        {
            return Result.Failure(TrackedMaterialUnitErrors.OperationalRequiresNonEmployee);
        }

        HolderType = newHolderType;
        HolderId = newHolderId;
        CustodyKind = newCustodyKind;
        RowVersion++;

        Raise(new TrackedMaterialUnitTransferredDomainEvent(
            Id,
            newHolderType,
            newHolderId,
            newCustodyKind,
            atUtc));

        return Result.Success();
    }

    private static Result ValidateIssue(
        Guid id,
        Guid materialId,
        string serialNumber,
        Guid warehouseId,
        PartyType holderType,
        Guid holderId,
        CustodyKind custodyKind,
        Guid issueDocumentId)
    {
        if (id == Guid.Empty || materialId == Guid.Empty || warehouseId == Guid.Empty ||
            holderId == Guid.Empty || issueDocumentId == Guid.Empty)
        {
            return Result.Failure(TrackedMaterialUnitErrors.IdentityRequired);
        }

        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return Result.Failure(TrackedMaterialUnitErrors.SerialNumberRequired);
        }

        if (!Enum.IsDefined(holderType))
        {
            return Result.Failure(TrackedMaterialUnitErrors.HolderTypeInvalid);
        }

        if (!Enum.IsDefined(custodyKind))
        {
            return Result.Failure(TrackedMaterialUnitErrors.CustodyKindInvalid);
        }

        if (custodyKind == CustodyKind.Personal && holderType != PartyType.Employee)
        {
            return Result.Failure(TrackedMaterialUnitErrors.PersonalRequiresEmployee);
        }

        if (custodyKind == CustodyKind.Operational && holderType == PartyType.Employee)
        {
            return Result.Failure(TrackedMaterialUnitErrors.OperationalRequiresNonEmployee);
        }

        return Result.Success();
    }
}
