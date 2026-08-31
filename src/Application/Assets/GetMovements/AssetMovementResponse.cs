namespace Application.Assets.GetMovements;

public sealed record AssetMovementResponse(
    Guid Id,
    Guid AssetId,
    Guid DocumentId,
    string DocumentReferenceNumber,
    string DocumentType,
    string MovementType,
    DateTime MovedAtUtc);
