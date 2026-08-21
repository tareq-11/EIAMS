using Domain.Common;
using SharedKernel;

namespace Domain.Custodies;

public sealed class Custody : Entity, IAuditableEntity
{
    private Custody() { }

    public Guid AssetId { get; private set; }
    public PartyType HolderType { get; private set; }
    public Guid HolderId { get; private set; }
    public CustodyKind CustodyKind { get; private set; }
    public Guid IssueDocumentId { get; private set; }
    public Guid? ReturnDocumentId { get; private set; }
    public Guid? DisposalDocumentId { get; private set; }
    public CustodyStatus Status { get; private set; }
    public DateTime FromUtc { get; private set; }
    public DateTime? ToUtc { get; private set; }
    public int RowVersion { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<Custody> Open(
        Guid id,
        Guid assetId,
        PartyType holderType,
        Guid holderId,
        CustodyKind custodyKind,
        Guid issueDocumentId,
        DateTime fromUtc)
    {
        Result validationResult = ValidateOpen(id, assetId, holderType, holderId, custodyKind, issueDocumentId);

        if (validationResult.IsFailure)
        {
            return Result.Failure<Custody>(validationResult.Error);
        }

        var custody = new Custody
        {
            Id = id,
            AssetId = assetId,
            HolderType = holderType,
            HolderId = holderId,
            CustodyKind = custodyKind,
            IssueDocumentId = issueDocumentId,
            Status = CustodyStatus.Active,
            FromUtc = fromUtc,
            RowVersion = 1
        };

        custody.Raise(new CustodyOpenedDomainEvent(id, assetId, holderType, holderId, custodyKind));

        return custody;
    }

    public Result Close(Guid? returnDocumentId, DateTime atUtc)
    {
        if (Status != CustodyStatus.Active)
        {
            return Result.Failure(CustodyErrors.NotActive);
        }

        if (returnDocumentId == Guid.Empty)
        {
            return Result.Failure(CustodyErrors.ReturnDocumentRequired);
        }

        if (atUtc <= FromUtc)
        {
            return Result.Failure(CustodyErrors.CloseTimeInvalid);
        }

        ReturnDocumentId = returnDocumentId;
        ToUtc = atUtc;
        Status = CustodyStatus.Closed;
        RowVersion++;
        Raise(new CustodyClosedDomainEvent(Id, AssetId, returnDocumentId));

        return Result.Success();
    }

    public Result Reopen()
    {
        if (Status != CustodyStatus.Closed)
        {
            return Result.Failure(CustodyErrors.StatusInvalid);
        }

        ReturnDocumentId = null;
        ToUtc = null;
        Status = CustodyStatus.Active;
        RowVersion++;
        Raise(new CustodyReopenedDomainEvent(Id, AssetId));

        return Result.Success();
    }

    public Result CloseForDisposal(Guid disposalDocumentId, DateTime atUtc)
    {
        if (Status != CustodyStatus.Active)
        {
            return Result.Failure(CustodyErrors.NotActive);
        }

        if (disposalDocumentId == Guid.Empty)
        {
            return Result.Failure(CustodyErrors.DisposalDocumentRequired);
        }

        if (atUtc <= FromUtc)
        {
            return Result.Failure(CustodyErrors.CloseTimeInvalid);
        }

        DisposalDocumentId = disposalDocumentId;
        ToUtc = atUtc;
        Status = CustodyStatus.Closed;
        RowVersion++;
        Raise(new CustodyClosedDomainEvent(Id, AssetId, null));
        return Result.Success();
    }

    private static Result ValidateOpen(
        Guid id,
        Guid assetId,
        PartyType holderType,
        Guid holderId,
        CustodyKind custodyKind,
        Guid issueDocumentId)
    {
        if (id == Guid.Empty || assetId == Guid.Empty || holderId == Guid.Empty || issueDocumentId == Guid.Empty)
        {
            return Result.Failure(CustodyErrors.IdentityRequired);
        }

        if (!Enum.IsDefined(holderType))
        {
            return Result.Failure(CustodyErrors.HolderTypeInvalid);
        }

        if (!Enum.IsDefined(custodyKind))
        {
            return Result.Failure(CustodyErrors.CustodyKindInvalid);
        }

        if (custodyKind == CustodyKind.Personal && holderType != PartyType.Employee)
        {
            return Result.Failure(CustodyErrors.PersonalRequiresEmployee);
        }

        return Result.Success();
    }
}
