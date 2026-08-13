using SharedKernel;

namespace Domain.ReceivingInfos;

public static class ReceivingInfoErrors
{
    public static Error Required(Guid documentId) => Error.Problem(
        "ReceivingInfo.Required",
        "Receiving information is required before the document can be submitted or posted.",
        new { document_id = documentId });

    public static Error WrongDocumentType(Guid documentId) => Error.Problem(
        "ReceivingInfo.WrongDocumentType",
        "Receiving information can only be attached to a Receiving document.",
        new { document_id = documentId });

    public static readonly Error SupplierRefInvalid = Error.Problem(
        "ReceivingInfo.SupplierRefInvalid",
        "Supplier reference is required and must not exceed 200 characters.");

    public static readonly Error SupplierInvoiceRefTooLong = Error.Problem(
        "ReceivingInfo.SupplierInvoiceRefTooLong",
        "Supplier invoice reference must not exceed 100 characters.");

    public static readonly Error ReceivingTypeInvalid = Error.Problem(
        "ReceivingInfo.ReceivingTypeInvalid",
        "ReceivingType must be a known value.");
}
