namespace Application.WarehouseDocuments.GetHistory;

public sealed record WarehouseDocumentHistoryResponse(
    Guid DocumentId,
    string CurrentStatus,
    int CurrentRowVersion,
    IReadOnlyList<DocumentLifecycleEventResponse> Events);

public sealed record DocumentLifecycleEventResponse(
    Guid EventId,
    string? FromStatus,
    string ToStatus,
    string Action,
    string? Reason,
    Guid? ActorUserId,
    string ActorDisplayName,
    DateTime OccurredAtUtc,
    int ResultingRowVersion,
    string? RequestId,
    Guid OperationId);
