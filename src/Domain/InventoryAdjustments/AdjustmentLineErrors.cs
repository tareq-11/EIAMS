using SharedKernel;

namespace Domain.InventoryAdjustments;

public static class AdjustmentLineErrors
{
    public static Error NotFound(Guid lineId) => Error.NotFound("AdjustmentLines.NotFound", "Adjustment line was not found.", new { line_id = lineId });
    public static readonly Error IdentityRequired = Error.Problem("AdjustmentLines.IdentityRequired", "Adjustment line identity values are required.");
    public static readonly Error DifferenceInvalid = Error.Problem("AdjustmentLines.DifferenceInvalid", "Difference must fit decimal(18,3).");
    public static readonly Error ZeroDifference = Error.Problem("AdjustmentLines.ZeroDifference", "Quantity adjustment difference cannot be zero.");
    public static readonly Error ReasonRequired = Error.Problem("AdjustmentLines.ReasonRequired", "Adjustment line reason is required.");
    public static readonly Error ReasonTooLong = Error.Problem("AdjustmentLines.ReasonTooLong", "Adjustment line reason cannot exceed 200 characters.");
    public static readonly Error AssetQuantityAdjustmentNotSupported = Error.Problem("AdjustmentLines.AssetQuantityAdjustmentNotSupported", "Asset quantities cannot be corrected with a normal quantity adjustment.");
    public static readonly Error DifferenceMustMatchDocumentLine = Error.Problem("AdjustmentLines.DifferenceMustMatchDocumentLine", "The absolute signed difference must match the document line base quantity.");
    public static Error MustUseDedicatedEndpoint(Guid lineId) => Error.Conflict(
        "AdjustmentLines.DedicatedEndpointRequired",
        "Adjustment lines must be changed through the inventory-adjustment line endpoint.",
        new { line_id = lineId });
    public static Error Duplicate(Guid documentId, Guid materialId) => Error.Conflict(
        "AdjustmentLines.Duplicate",
        "An adjustment line for this material already exists in the document.",
        new { document_id = documentId, material_id = materialId });
}
