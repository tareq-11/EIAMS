using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseDocuments;

/// <summary>
/// The shared operational-document header (Spine) every posted stock movement traces back to
/// (Ch. 4, D-WF-01). Only Draft documents are editable; state can only change through the methods
/// below, each of which enforces its allowed source state and increments <see cref="RowVersion"/>
/// on a real mutation (see M3-PLAN.md §1.1/§1.7).
/// </summary>
public sealed class WarehouseDocument : Entity, IAuditableEntity
{
    private WarehouseDocument() { }

    public Guid WarehouseId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string? PaperDocumentNumber { get; private set; }
    public int? PaperDocumentYear { get; private set; }
    public string SystemReferenceNumber { get; private set; }
    public Guid? SignedCopyAttachmentId { get; private set; }
    public DocumentStatus DocumentStatus { get; private set; }
    public Guid? PostedBy { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public Guid? ReversalOfDocumentId { get; private set; }
    public int RowVersion { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static WarehouseDocument CreateDraft(
        Guid id,
        Guid warehouseId,
        DocumentType documentType,
        string systemReferenceNumber,
        Guid? reversalOfDocumentId = null)
    {
        var document = new WarehouseDocument
        {
            Id = id,
            WarehouseId = warehouseId,
            DocumentType = documentType,
            SystemReferenceNumber = systemReferenceNumber,
            DocumentStatus = DocumentStatus.Draft,
            ReversalOfDocumentId = reversalOfDocumentId,
            RowVersion = 1
        };

        document.Raise(new WarehouseDocumentCreatedDomainEvent(
            document.Id,
            warehouseId,
            documentType,
            reversalOfDocumentId));

        return document;
    }

    public Result UpdatePaperReference(string? paperDocumentNumber, int? paperDocumentYear)
    {
        if (DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure(WarehouseDocumentErrors.NotEditable(Id, DocumentStatus));
        }

        if (PaperDocumentNumber == paperDocumentNumber && PaperDocumentYear == paperDocumentYear)
        {
            return Result.Success();
        }

        PaperDocumentNumber = paperDocumentNumber;
        PaperDocumentYear = paperDocumentYear;
        RegisterDetailMutation();

        Raise(new WarehouseDocumentPaperReferenceUpdatedDomainEvent(Id));

        return Result.Success();
    }

    public Result SetSignedCopy(Guid attachmentId)
    {
        if (DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure(WarehouseDocumentErrors.NotEditable(Id, DocumentStatus));
        }

        if (SignedCopyAttachmentId == attachmentId)
        {
            return Result.Success();
        }

        SignedCopyAttachmentId = attachmentId;
        RegisterDetailMutation();

        Raise(new WarehouseDocumentSignedCopySetDomainEvent(Id, attachmentId));

        return Result.Success();
    }

    public Result RemoveSignedCopy()
    {
        if (DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure(WarehouseDocumentErrors.NotEditable(Id, DocumentStatus));
        }

        if (SignedCopyAttachmentId is null)
        {
            return Result.Success();
        }

        SignedCopyAttachmentId = null;
        RegisterDetailMutation();

        Raise(new WarehouseDocumentSignedCopyRemovedDomainEvent(Id));

        return Result.Success();
    }

    /// <summary>
    /// Advances the optimistic-concurrency token after a real Draft detail mutation such as a
    /// line or supporting-attachment change.
    /// </summary>
    public Result RegisterDetailMutation()
    {
        if (DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure(WarehouseDocumentErrors.NotEditable(Id, DocumentStatus));
        }

        RowVersion++;

        return Result.Success();
    }

    public Result Submit()
    {
        if (DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure(WarehouseDocumentErrors.InvalidTransition(Id, DocumentStatus, DocumentStatus.Submitted));
        }

        if (string.IsNullOrWhiteSpace(PaperDocumentNumber) || PaperDocumentYear is null)
        {
            return Result.Failure(WarehouseDocumentErrors.PaperReferenceRequired(Id));
        }

        DocumentStatus = DocumentStatus.Submitted;
        RowVersion++;

        Raise(new WarehouseDocumentSubmittedDomainEvent(Id));

        return Result.Success();
    }

    public Result Reject()
    {
        if (DocumentStatus != DocumentStatus.Submitted)
        {
            return Result.Failure(WarehouseDocumentErrors.InvalidTransition(Id, DocumentStatus, DocumentStatus.Rejected));
        }

        DocumentStatus = DocumentStatus.Rejected;
        RowVersion++;

        Raise(new WarehouseDocumentRejectedDomainEvent(Id));

        return Result.Success();
    }

    public Result ReturnToDraft()
    {
        if (DocumentStatus != DocumentStatus.Rejected)
        {
            return Result.Failure(WarehouseDocumentErrors.InvalidTransition(Id, DocumentStatus, DocumentStatus.Draft));
        }

        DocumentStatus = DocumentStatus.Draft;
        RowVersion++;

        Raise(new WarehouseDocumentReturnedToDraftDomainEvent(Id));

        return Result.Success();
    }

    public Result Cancel()
    {
        if (DocumentStatus is not (DocumentStatus.Draft or DocumentStatus.Submitted or DocumentStatus.Rejected))
        {
            return Result.Failure(WarehouseDocumentErrors.InvalidTransition(Id, DocumentStatus, DocumentStatus.Cancelled));
        }

        DocumentStatus = DocumentStatus.Cancelled;
        RowVersion++;

        Raise(new WarehouseDocumentCancelledDomainEvent(Id));

        return Result.Success();
    }

    public Result MarkPosted(Guid postedBy, DateTime postedAtUtc)
    {
        Result validationResult = ValidateForPosting();

        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        DocumentStatus = DocumentStatus.Posted;
        PostedBy = postedBy;
        PostedAtUtc = postedAtUtc;
        RowVersion++;

        Raise(new WarehouseDocumentPostedDomainEvent(Id, WarehouseId, DocumentType));

        return Result.Success();
    }

    public Result ValidateForPosting()
    {
        if (DocumentStatus != DocumentStatus.Submitted)
        {
            return Result.Failure(WarehouseDocumentErrors.InvalidTransition(Id, DocumentStatus, DocumentStatus.Posted));
        }

        if (SignedCopyAttachmentId is null)
        {
            return Result.Failure(WarehouseDocumentErrors.SignedCopyRequired(Id));
        }

        return Result.Success();
    }

    public Result MarkReversed()
    {
        if (DocumentStatus != DocumentStatus.Posted)
        {
            return Result.Failure(WarehouseDocumentErrors.InvalidTransition(Id, DocumentStatus, DocumentStatus.Reversed));
        }

        DocumentStatus = DocumentStatus.Reversed;
        RowVersion++;

        Raise(new WarehouseDocumentReversedDomainEvent(Id));

        return Result.Success();
    }
}
