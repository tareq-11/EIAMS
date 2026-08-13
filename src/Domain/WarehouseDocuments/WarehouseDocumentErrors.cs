using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseDocuments;

public static class WarehouseDocumentErrors
{
    public static Error NotFound(Guid documentId) => Error.NotFound(
        "WarehouseDocuments.NotFound",
        $"The warehouse document with the Id = '{documentId}' was not found",
        new { document_id = documentId });

    public static Error InvalidTransition(Guid documentId, DocumentStatus from, DocumentStatus to) => Error.Problem(
        "WarehouseDocuments.InvalidTransition",
        $"Cannot move the document from '{from}' to '{to}'.",
        new { document_id = documentId, from_status = from.ToString(), to_status = to.ToString() });

    public static Error NotEditable(Guid documentId, DocumentStatus currentStatus) => Error.Problem(
        "WarehouseDocuments.NotEditable",
        $"The document with the Id = '{documentId}' is '{currentStatus}' and can only be edited while Draft.",
        new { document_id = documentId, current_status = currentStatus.ToString() });

    public static Error RowVersionMismatch(Guid documentId, int expectedRowVersion, int? currentRowVersion) =>
        Error.Conflict(
            "WarehouseDocuments.RowVersionMismatch",
            "The document was modified by another request; reload and try again.",
            new
            {
                document_id = documentId,
                expected_row_version = expectedRowVersion,
                current_row_version = currentRowVersion
            });

    public static Error LinesRequired(Guid documentId) => Error.Problem(
        "WarehouseDocuments.LinesRequired",
        $"The document with the Id = '{documentId}' must have at least one line before it can be submitted.",
        new { document_id = documentId });

    public static Error PaperReferenceRequired(Guid documentId) => Error.Problem(
        "WarehouseDocuments.PaperReferenceRequired",
        $"The document with the Id = '{documentId}' requires a paper document number and year before it can be submitted.",
        new { document_id = documentId });

    public static Error SignedCopyRequired(Guid documentId) => Error.Problem(
        "WarehouseDocuments.SignedCopyRequired",
        $"The document with the Id = '{documentId}' requires a SignedOriginal attachment before it can be posted.",
        new { document_id = documentId });

    public static Error PostingStrategyNotAvailable(Guid documentId, DocumentType documentType) => Error.Problem(
        "WarehouseDocuments.PostingStrategyNotAvailable",
        $"No posting strategy is registered yet for document type '{documentType}'.",
        new { document_id = documentId, document_type = documentType.ToString() });

    public static Error AlreadyReversed(Guid documentId) => Error.Conflict(
        "WarehouseDocuments.AlreadyReversed",
        $"The document with the Id = '{documentId}' has already been targeted by a reversal.",
        new { document_id = documentId });

    public static Error NotEligibleForReversal(Guid documentId, DocumentStatus currentStatus) => Error.Problem(
        "WarehouseDocuments.NotEligibleForReversal",
        $"The document with the Id = '{documentId}' is '{currentStatus}' and cannot be reversed; only a Posted, non-reversal document can be.",
        new { document_id = documentId, current_status = currentStatus.ToString() });

    public static Error ReversalLineMismatch(Guid documentId) => Error.Problem(
        "WarehouseDocuments.ReversalLineMismatch",
        $"The reversal document with the Id = '{documentId}' does not exactly match its source lines.",
        new { document_id = documentId });

    public static Error ReversalLinesImmutable(Guid documentId) => Error.Problem(
        "WarehouseDocuments.ReversalLinesImmutable",
        $"The copied lines of reversal document '{documentId}' cannot be added, edited, or removed.",
        new { document_id = documentId });

    public static readonly Error Forbidden = Error.Forbidden(
        "WarehouseDocuments.Forbidden",
        "You are not authorized to manage warehouse documents.");
}
