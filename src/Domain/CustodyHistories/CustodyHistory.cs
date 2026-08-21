using Domain.Common;
using SharedKernel;

namespace Domain.CustodyHistories;

public sealed class CustodyHistory : Entity, IAuditableEntity
{
    private CustodyHistory() { }

    public Guid CustodyId { get; private set; }
    public CustodyStatus FromStatus { get; private set; }
    public CustodyStatus ToStatus { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateTime AtUtc { get; private set; }
    public string? Note { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<CustodyHistory> Create(
        Guid id,
        Guid custodyId,
        CustodyStatus fromStatus,
        CustodyStatus toStatus,
        Guid changedBy,
        DateTime atUtc,
        string? note)
    {
        if (id == Guid.Empty || custodyId == Guid.Empty || changedBy == Guid.Empty)
        {
            return Result.Failure<CustodyHistory>(CustodyHistoryErrors.IdentityRequired);
        }

        if (!Enum.IsDefined(fromStatus) || !Enum.IsDefined(toStatus))
        {
            return Result.Failure<CustodyHistory>(CustodyHistoryErrors.StatusInvalid);
        }

        if (fromStatus == toStatus)
        {
            return Result.Failure<CustodyHistory>(CustodyHistoryErrors.TransitionInvalid);
        }

        string? normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (normalizedNote?.Length > 300)
        {
            return Result.Failure<CustodyHistory>(CustodyHistoryErrors.NoteTooLong);
        }

        var history = new CustodyHistory
        {
            Id = id,
            CustodyId = custodyId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedBy = changedBy,
            AtUtc = atUtc,
            Note = normalizedNote
        };

        history.Raise(new CustodyHistoryRecordedDomainEvent(id, custodyId, fromStatus, toStatus));

        return history;
    }
}
