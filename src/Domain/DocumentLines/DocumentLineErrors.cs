using Domain.Common;
using SharedKernel;

namespace Domain.DocumentLines;

public static class DocumentLineErrors
{
    public static Error NotFound(Guid lineId) => Error.NotFound(
        "DocumentLines.NotFound",
        $"The document line with the Id = '{lineId}' was not found",
        new { line_id = lineId });

    public static readonly Error QuantityMustBePositive = Error.Problem(
        "DocumentLines.QuantityMustBePositive",
        "Quantity must be greater than zero.");

    public static readonly Error BaseQuantityMustBePositive = Error.Problem(
        "DocumentLines.BaseQuantityMustBePositive",
        "BaseQuantity must be greater than zero.");

    public static readonly Error UnitPriceMustBeNonNegative = Error.Problem(
        "DocumentLines.UnitPriceMustBeNonNegative",
        "UnitPrice, when provided, must be non-negative.");

    public static Error MaterialNotActive(Guid materialId) => Error.Problem(
        "DocumentLines.MaterialNotActive",
        $"The material with the Id = '{materialId}' must be active to be used on a document line.",
        new { material_id = materialId });

    public static Error MaterialFamilyNotActive(Guid familyId) => Error.Problem(
        "DocumentLines.MaterialFamilyNotActive",
        $"The material family with the Id = '{familyId}' must be active to be used on a document line.",
        new { family_id = familyId });

    public static Error MaterialCategoryNotActive(Guid categoryId) => Error.Problem(
        "DocumentLines.MaterialCategoryNotActive",
        $"The material category with the Id = '{categoryId}' must be active to be used on a document line.",
        new { category_id = categoryId });

    public static Error MaterialDomainNotActive(Guid materialDomainId) => Error.Problem(
        "DocumentLines.MaterialDomainNotActive",
        $"The material domain with the Id = '{materialDomainId}' must be active to be used on a document line.",
        new { material_domain_id = materialDomainId });

    public static Error UnitNotFound(Guid unitId) => Error.NotFound(
        "DocumentLines.UnitNotFound",
        $"The unit of measure with the Id = '{unitId}' was not found.",
        new { unit_id = unitId });

    public static Error UnitConversionNotFound(Guid materialId, Guid unitId) => Error.NotFound(
        "DocumentLines.UnitConversionNotFound",
        $"No active unit conversion exists from unit '{unitId}' to the material's base unit for material '{materialId}'.",
        new { material_id = materialId, unit_id = unitId });

    public static readonly Error BaseQuantityOverflow = Error.Problem(
        "DocumentLines.BaseQuantityOverflow",
        "The computed base quantity does not fit within decimal(18,3).");

    public static readonly Error QuantityPrecisionInvalid = Error.Problem(
        "DocumentLines.QuantityPrecisionInvalid",
        "Quantity must fit within decimal(18,3).");

    public static readonly Error UnitPricePrecisionInvalid = Error.Problem(
        "DocumentLines.UnitPricePrecisionInvalid",
        "UnitPrice must fit within decimal(18,2).");

    public static Error OpeningTypeRequired(Guid documentId) => Error.Problem(
        "DocumentLines.OpeningTypeRequired",
        "OpeningType is required for every Opening document line.",
        new { document_id = documentId });

    public static Error OpeningTypeNotAllowed(Guid documentId) => Error.Problem(
        "DocumentLines.OpeningTypeNotAllowed",
        "OpeningType can only be supplied for an Opening document line.",
        new { document_id = documentId });

    public static Error OpeningTypeInvalid(Guid documentId, OpeningType openingType) => Error.Problem(
        "DocumentLines.OpeningTypeInvalid",
        "OpeningType must be a known value.",
        new { document_id = documentId, opening_type = (int)openingType });

    public static Error AssetQuantityMustBeWhole(Guid lineId, decimal baseQuantity) => Error.Problem(
        "DocumentLines.AssetQuantityMustBeWhole",
        "An asset-tracked line must contain a whole number of base units.",
        new { line_id = lineId, base_quantity = baseQuantity });

    public static Error AssetQuantityLimitExceeded(Guid lineId, decimal baseQuantity, int maximum) => Error.Problem(
        "DocumentLines.AssetQuantityLimitExceeded",
        $"An asset-tracked line cannot create more than {maximum} assets.",
        new { line_id = lineId, base_quantity = baseQuantity, maximum });

    public static Error AssetDocumentLimitExceeded(Guid documentId, decimal totalAssets, int maximum) => Error.Problem(
        "DocumentLines.AssetDocumentLimitExceeded",
        $"A document cannot create more than {maximum} assets.",
        new { document_id = documentId, total_assets = totalAssets, maximum });

    public static Error LinesLimitExceeded(Guid documentId, int lineCount, int maximum) => Error.Problem(
        "DocumentLines.LinesLimitExceeded",
        $"A document cannot contain more than {maximum} lines.",
        new { document_id = documentId, line_count = lineCount, maximum });

    public static Error BaseQuantityMismatch(
        Guid documentId,
        Guid lineId,
        decimal storedBaseQuantity,
        decimal expectedBaseQuantity) => Error.Problem(
            "DocumentLines.BaseQuantityMismatch",
            "The stored base quantity no longer matches the active unit conversion.",
            new
            {
                document_id = documentId,
                line_id = lineId,
                stored_base_quantity = storedBaseQuantity,
                expected_base_quantity = expectedBaseQuantity
            });

    public static Error LineTypeMismatch(
        Guid documentId,
        Guid lineId,
        DocumentLineType currentLineType,
        DocumentLineType expectedLineType) => Error.Problem(
            "DocumentLines.LineTypeMismatch",
            "The document line type no longer matches the material kind.",
            new
            {
                document_id = documentId,
                line_id = lineId,
                current_line_type = currentLineType.ToString(),
                expected_line_type = expectedLineType.ToString()
            });

    public static Error MaterialProvenanceStale(
        Guid documentId,
        Guid lineId,
        Guid materialId,
        int capturedMaterialVersion,
        int currentMaterialVersion) => Error.Conflict(
            "DocumentLines.MaterialProvenanceStale",
            $"The line was captured against material catalog version {capturedMaterialVersion}, but the material is now at version {currentMaterialVersion}. Update the line to re-capture its provenance.",
            new
            {
                document_id = documentId,
                line_id = lineId,
                material_id = materialId,
                captured_material_version = capturedMaterialVersion,
                current_material_version = currentMaterialVersion
            });

    public static Error BaseUnitProvenanceStale(
        Guid documentId,
        Guid lineId,
        Guid materialId,
        Guid capturedBaseUnitId,
        Guid currentBaseUnitId) => Error.Conflict(
            "DocumentLines.BaseUnitProvenanceStale",
            "The line was captured against a different material base unit. Update the line to re-capture its provenance.",
            new
            {
                document_id = documentId,
                line_id = lineId,
                material_id = materialId,
                captured_base_unit_id = capturedBaseUnitId,
                current_base_unit_id = currentBaseUnitId
            });

    public static Error ClassificationProvenanceStale(
        Guid documentId,
        Guid lineId,
        Guid materialId,
        string capturedClassification,
        string currentClassification) => Error.Conflict(
            "DocumentLines.ClassificationProvenanceStale",
            "The line was captured against a different material classification. Update the line to re-capture its provenance.",
            new
            {
                document_id = documentId,
                line_id = lineId,
                material_id = materialId,
                captured_classification = capturedClassification,
                current_classification = currentClassification
            });

    public static Error ConversionProvenanceStale(
        Guid documentId,
        Guid lineId,
        Guid materialId,
        Guid? capturedConversionId,
        Guid? currentConversionId) => Error.Conflict(
            "DocumentLines.ConversionProvenanceStale",
            "The unit conversion captured with the line is no longer the active conversion for its unit. Update the line to re-capture its provenance.",
            new
            {
                document_id = documentId,
                line_id = lineId,
                material_id = materialId,
                captured_conversion_id = capturedConversionId,
                current_conversion_id = currentConversionId
            });

    public static Error ConversionProvenanceChanged(
        Guid documentId,
        Guid lineId,
        Guid conversionId,
        decimal capturedFactor,
        decimal currentFactor) => Error.Conflict(
            "DocumentLines.ConversionProvenanceChanged",
            "The unit conversion factor used by the line has changed since the line was captured. Update the line to re-capture its provenance.",
            new
            {
                document_id = documentId,
                line_id = lineId,
                conversion_id = conversionId,
                captured_factor = capturedFactor,
                current_factor = currentFactor
            });

    /// <summary>
    /// The line carries no catalog snapshot, so its material classification revision, base unit and
    /// conversion factor were never recorded and cannot be proven at submit or post. It is refused
    /// instead of being re-interpreted against the live catalog; re-capturing the line (updating it
    /// while its document is still a draft) is the only way forward, which re-reads the live catalog
    /// deliberately instead of guessing what the line used to mean.
    /// </summary>
    public static Error ProvenanceNotCaptured(Guid documentId, Guid lineId) => Error.Conflict(
        "DocumentLines.ProvenanceNotCaptured",
        "The document line was created before material provenance was captured, so the catalog revision it was written against is unknown and it cannot be submitted or posted. Update the line to re-capture its provenance against the current catalog, or cancel the document.",
        new { document_id = documentId, line_id = lineId });

    /// <summary>
    /// The stored snapshot is incomplete: a catalog revision was recorded but part of the snapshot is
    /// missing. This is a corrupt row rather than a legacy fact, so it is reported distinctly instead
    /// of being treated as "never captured".
    /// </summary>
    public static Error ProvenanceIncomplete(Guid documentId, Guid lineId, int? storedMaterialVersion) =>
        Error.Conflict(
            "DocumentLines.ProvenanceIncomplete",
            "The document line stores a partial material provenance snapshot, which cannot be interpreted. Update the line to re-capture its provenance against the current catalog.",
            new
            {
                document_id = documentId,
                line_id = lineId,
                stored_material_version = storedMaterialVersion
            });

    /// <summary>An incomplete provenance capture was rejected before it could be stored.</summary>
    public static Error ProvenanceCaptureInvalid(string field, string reason) => Error.Problem(
        "DocumentLines.ProvenanceCaptureInvalid",
        $"The captured material provenance is incomplete: {field} {reason}.",
        new { field });
}
