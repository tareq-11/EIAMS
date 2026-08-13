using SharedKernel;

namespace Domain.StockMovements;

public static class StockMovementErrors
{
    public static readonly Error DeltaMustNotBeZero = Error.Problem(
        "StockMovements.DeltaMustNotBeZero",
        "A stock movement's quantity delta must not be zero.");

    public static Error DuplicatePosting(Guid documentId, Guid lineId) => Error.Conflict(
        "StockMovements.DuplicatePosting",
        "A movement of this type already exists for this document line; posting cannot be repeated.",
        new { document_id = documentId, line_id = lineId });

    public static Error DuplicatePosting(Guid documentId) => Error.Conflict(
        "StockMovements.DuplicatePosting",
        "One or more movements already exist for this document; posting cannot be repeated.",
        new { document_id = documentId });
}
