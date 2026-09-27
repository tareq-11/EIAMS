using Domain.DocumentLines;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using SharedKernel;

namespace Application.DocumentLines;

/// <summary>
/// Re-validates the catalog snapshot a draft line froze (<see cref="DocumentLine.Provenance"/>) at
/// submit and at post. A document line is an accounting fact, so once it exists the catalog may
/// change but the line's meaning may not: any difference in the material's classification revision,
/// base unit, or the unit conversion that produced its base quantity blocks the transition with an
/// explicit error instead of silently re-interpreting the document.
/// <para>
/// This check is only reached from the operational (non-reversal) validation path, so a reversal of
/// already-posted history keeps posting even when the source lines predate provenance capture - their
/// meaning is a historical fact that the live catalog no longer has to support.
/// </para>
/// <para>
/// A line that carries no usable snapshot is refused rather than validated against the live catalog:
/// its material version, base unit and conversion factor were never recorded, so the current catalog
/// is not evidence of what the line meant. The remediation is explicit - re-capture the line by
/// updating it while the document is still a draft, or cancel the document - and no historical
/// conversion data is ever guessed to close the gap.
/// </para>
/// </summary>
internal static class DocumentLineProvenanceRules
{
    public static Result Validate(
        Guid documentId,
        DocumentLine line,
        Material material,
        MaterialUnitConversion? currentConversion)
    {
        Result<DocumentLineProvenance> captured = line.RequireProvenance();

        if (captured.IsFailure)
        {
            return Result.Failure(captured.Error);
        }

        DocumentLineProvenance snapshot = captured.Value;

        if (snapshot.MaterialVersion != material.CatalogVersion)
        {
            return Result.Failure(DocumentLineErrors.MaterialProvenanceStale(
                documentId,
                line.Id,
                material.Id,
                snapshot.MaterialVersion,
                material.CatalogVersion));
        }

        if (snapshot.BaseUnitId != material.BaseUnitId)
        {
            return Result.Failure(DocumentLineErrors.BaseUnitProvenanceStale(
                documentId,
                line.Id,
                material.Id,
                snapshot.BaseUnitId,
                material.BaseUnitId));
        }

        if (snapshot.MaterialKind != material.MaterialKind || snapshot.TrackingType != material.TrackingType)
        {
            return Result.Failure(DocumentLineErrors.ClassificationProvenanceStale(
                documentId,
                line.Id,
                material.Id,
                Describe(snapshot.MaterialKind, snapshot.TrackingType),
                Describe(material.MaterialKind, material.TrackingType)));
        }

        return ValidateConversion(documentId, line, material, snapshot, currentConversion);
    }

    private static Result ValidateConversion(
        Guid documentId,
        DocumentLine line,
        Material material,
        DocumentLineProvenance captured,
        MaterialUnitConversion? currentConversion)
    {
        if (captured.ConversionId != currentConversion?.Id)
        {
            return Result.Failure(DocumentLineErrors.ConversionProvenanceStale(
                documentId,
                line.Id,
                material.Id,
                captured.ConversionId,
                currentConversion?.Id));
        }

        if (currentConversion is null)
        {
            return Result.Success();
        }

        bool identityChanged = captured.ConversionFromUnitId != currentConversion.FromUnitId ||
            captured.ConversionToUnitId != currentConversion.ToBaseUnitId;

        if (identityChanged || captured.ConversionFactor != currentConversion.Factor)
        {
            return Result.Failure(DocumentLineErrors.ConversionProvenanceChanged(
                documentId,
                line.Id,
                currentConversion.Id,
                captured.ConversionFactor ?? 0m,
                currentConversion.Factor));
        }

        return Result.Success();
    }

    private static string Describe(MaterialKind materialKind, TrackingType trackingType) =>
        $"{materialKind}/{trackingType}";
}
