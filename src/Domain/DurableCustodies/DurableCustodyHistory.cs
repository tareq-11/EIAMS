using Domain.Common;
using SharedKernel;

namespace Domain.DurableCustodies;

public sealed class DurableCustodyHistory : Entity, IAuditableEntity
{
    private DurableCustodyHistory() { }

    public CustodySubjectType SubjectType { get; private set; }
    public Guid SubjectId { get; private set; }
    public string Action { get; private set; }
    public PartyType? FromHolderType { get; private set; }
    public Guid? FromHolderId { get; private set; }
    public PartyType? ToHolderType { get; private set; }
    public Guid? ToHolderId { get; private set; }
    public decimal? Quantity { get; private set; }
    public Guid? DocumentId { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public Guid ActorId { get; private set; }
    public string? Note { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static DurableCustodyHistory Record(
        Guid id,
        CustodySubjectType subjectType,
        Guid subjectId,
        string action,
        PartyType? fromHolderType,
        Guid? fromHolderId,
        PartyType? toHolderType,
        Guid? toHolderId,
        decimal? quantity,
        Guid? documentId,
        DateTime timestampUtc,
        Guid actorId,
        string? note = null)
    {
        return new DurableCustodyHistory
        {
            Id = id,
            SubjectType = subjectType,
            SubjectId = subjectId,
            Action = action,
            FromHolderType = fromHolderType,
            FromHolderId = fromHolderId,
            ToHolderType = toHolderType,
            ToHolderId = toHolderId,
            Quantity = quantity,
            DocumentId = documentId,
            TimestampUtc = timestampUtc,
            ActorId = actorId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
    }
}
