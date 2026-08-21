namespace Application.Abstractions.Posting;

public sealed record PostingWarning(
    string Code,
    string Message,
    Guid CountId,
    Guid WarehouseId);

public sealed record PostingOutcome(
    Guid DocumentId,
    IReadOnlyList<PostingWarning> Warnings);
