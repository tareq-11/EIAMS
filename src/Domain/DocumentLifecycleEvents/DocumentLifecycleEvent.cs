using Domain.Common;
using SharedKernel;

namespace Domain.DocumentLifecycleEvents;

/// <summary>
/// Immutable evidence of an actual warehouse-document state transition.
/// </summary>
public sealed class DocumentLifecycleEvent : Entity
{
    private DocumentLifecycleEvent() { }

    public Guid DocumentId { get; private set; }
    public DocumentStatus? FromStatus { get; private set; }
    public DocumentStatus ToStatus { get; private set; }
    public string Action { get; private set; }
    public string? Reason { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorDisplayName { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public int ResultingRowVersion { get; private set; }
    public string? RequestId { get; private set; }
    public Guid OperationId { get; private set; }

    public static DocumentLifecycleEvent Create(
        Guid id,
        Guid documentId,
        DocumentStatus? fromStatus,
        DocumentStatus toStatus,
        string action,
        string? reason,
        Guid? actorUserId,
        string? actorDisplayName,
        DateTime occurredAtUtc,
        int resultingRowVersion,
        string? requestId,
        Guid operationId) => new()
        {
            Id = id,
            DocumentId = documentId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Action = action,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ActorUserId = actorUserId,
            ActorDisplayName = string.IsNullOrWhiteSpace(actorDisplayName) ? "System" : actorDisplayName.Trim(),
            OccurredAtUtc = occurredAtUtc,
            ResultingRowVersion = resultingRowVersion,
            RequestId = string.IsNullOrWhiteSpace(requestId) ? null : requestId,
            OperationId = operationId
        };
}
