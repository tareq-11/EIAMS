using SharedKernel;

namespace Domain.WarehouseDocuments;

public static class OpeningDocumentErrors
{
    public static Error CorrectionRequiresAdjustment(Guid documentId, Guid lineId) => Error.Problem(
        "OpeningDocuments.CorrectionRequiresAdjustment",
        "Opening corrections must be recorded through an Adjustment document.",
        new { document_id = documentId, line_id = lineId });

    public static Error DuplicateMaterial(Guid documentId, Guid materialId) => Error.Problem(
        "OpeningDocuments.DuplicateMaterial",
        "An Opening document can contain a material only once.",
        new { document_id = documentId, material_id = materialId });

    public static Error AlreadyInitialized(Guid warehouseId, Guid materialId) => Error.Conflict(
        "OpeningDocuments.AlreadyInitialized",
        "This warehouse and material already have stock history and cannot receive an Initial Opening balance.",
        new { warehouse_id = warehouseId, material_id = materialId });
}
