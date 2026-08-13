using SharedKernel;

namespace Domain.TransferInfos;

public static class TransferInfoErrors
{
    public static Error Required(Guid documentId) => Error.Problem(
        "TransferInfos.Required",
        "Transfer destination information is required before the document can be submitted or posted.",
        new { document_id = documentId });

    public static Error WrongDocumentType(Guid documentId) => Error.Problem(
        "TransferInfos.WrongDocumentType",
        "Transfer information can only be attached to a Transfer document.",
        new { document_id = documentId });

    public static Error DestinationSameAsSource(Guid documentId, Guid warehouseId) => Error.Problem(
        "TransferInfos.DestinationSameAsSource",
        "Transfer destination must differ from the source warehouse.",
        new { document_id = documentId, warehouse_id = warehouseId });

    public static readonly Error DestinationRequired = Error.Problem(
        "TransferInfos.DestinationRequired",
        "DestinationWarehouseId is required.");

    public static readonly Error TransferReasonInvalid = Error.Problem(
        "TransferInfos.TransferReasonInvalid",
        "Transfer reason is required and must not exceed 200 characters.");

    public static Error AssetLinesNotSupported(Guid documentId) => Error.Problem(
        "TransferInfos.AssetLinesNotSupported",
        "Asset-tracked lines cannot be transferred until asset custody transfer is available.",
        new { document_id = documentId });
}
