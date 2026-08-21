using SharedKernel;

namespace Domain.ReturnInfos;

public static class ReturnInfoErrors
{
    public static Error Required(Guid documentId) => Error.Problem(
        "ReturnInfos.Required",
        "Return information is required before the document can be submitted or posted.",
        new { document_id = documentId });

    public static Error WrongDocumentType(Guid documentId) => Error.Problem(
        "ReturnInfos.WrongDocumentType",
        "Return information can only be attached to a Return document.",
        new { document_id = documentId });

    public static readonly Error OriginalIssueRequired = Error.Problem(
        "ReturnInfos.OriginalIssueRequired",
        "Original issue document is required.");

    public static readonly Error ReturnReasonInvalid = Error.Problem(
        "ReturnInfos.ReturnReasonInvalid",
        "Return reason is required and must not exceed 200 characters.");

    public static Error OriginalIssueInvalid(Guid originalIssueDocumentId) => Error.Problem(
        "ReturnInfos.OriginalIssueInvalid",
        "Original issue document must be a valid posted Issue document.",
        new { original_issue_document_id = originalIssueDocumentId });

    public static Error WrongWarehouse(Guid documentId, Guid warehouseId) => Error.Problem(
        "ReturnInfos.WrongWarehouse",
        "Return document warehouse must match the original issue warehouse.",
        new { document_id = documentId, warehouse_id = warehouseId });
}
