using SharedKernel;

namespace Domain.DocumentLineAssetSelections;

public static class DocumentLineAssetSelectionErrors
{
    public static readonly Error IdentityRequired = Error.Problem("DocumentLineAssetSelections.IdentityRequired", "Asset selection identity values are required.");
    public static Error NotFound(Guid documentLineId, Guid assetId) => Error.NotFound("DocumentLineAssetSelections.NotFound", "Asset selection was not found.", new { document_line_id = documentLineId, asset_id = assetId });
    public static Error Duplicate(Guid documentId, Guid assetId) => Error.Conflict("DocumentLineAssetSelections.Duplicate", "The asset is already selected in this document.", new { document_id = documentId, asset_id = assetId });
    public static Error CountMismatch(Guid lineId, decimal expected, int actual) => Error.Problem("DocumentLineAssetSelections.CountMismatch", "The selected asset count must match the asset line quantity.", new { line_id = lineId, expected_quantity = expected, actual_count = actual });
    public static Error AssetNotForLineMaterial(Guid assetId, Guid lineId) => Error.Problem("DocumentLineAssetSelections.AssetNotForLineMaterial", "Selected asset material does not match the document line.", new { asset_id = assetId, line_id = lineId });
    public static Error AssetNotInSourceWarehouse(Guid assetId, Guid warehouseId) => Error.Problem("DocumentLineAssetSelections.AssetNotInSourceWarehouse", "Selected asset is not in the document source warehouse.", new { asset_id = assetId, warehouse_id = warehouseId });
    public static Error AssetNotInStock(Guid assetId) => Error.Conflict("DocumentLineAssetSelections.AssetNotInStock", "Selected asset is not currently in stock.", new { asset_id = assetId });
    public static Error UnsupportedDocumentType(Guid documentId) => Error.Problem("DocumentLineAssetSelections.UnsupportedDocumentType", "Asset selections are supported only for Issue and Return documents.", new { document_id = documentId });
    public static Error UnsupportedLineType(Guid lineId) => Error.Problem("DocumentLineAssetSelections.UnsupportedLineType", "Asset selections require an Asset document line.", new { line_id = lineId });
    public static Error ActiveCustodyMismatch(Guid assetId, Guid issueDocumentId) => Error.Problem("DocumentLineAssetSelections.ActiveCustodyMismatch", "Selected asset does not have the required active custody.", new { asset_id = assetId, issue_document_id = issueDocumentId });
    public static Error LineHasSelections(Guid lineId) => Error.Problem("DocumentLineAssetSelections.LineHasSelections", "Remove the selected assets before removing the document line.", new { line_id = lineId });
}
