using Domain.Materials;
using SharedKernel;

namespace Domain.DocumentLines;

/// <summary>
/// Immutable snapshot of the catalog state a document line was written against (3B provenance).
/// <para>
/// It is captured while the owning document is still a draft and is validated again at submit and
/// at post, so a later catalog edit (classification, base unit, conversion factor) can never
/// silently reinterpret a document. A line whose unit is the material base unit carries no
/// conversion fields; a line written in another unit carries the exact conversion identity and
/// factor that produced its base quantity.
/// </para>
/// <para>
/// A snapshot is all-or-nothing by construction: the members are non-nullable, so a partial capture
/// cannot be expressed, and <see cref="Create"/> refuses one instead of defaulting the missing
/// classification or base unit. A line that has no snapshot at all is an explicit legacy fact
/// (<c>DocumentLine.Provenance == null</c>), never a partially filled one.
/// </para>
/// </summary>
public sealed record DocumentLineProvenance(
    int MaterialVersion,
    MaterialKind MaterialKind,
    TrackingType TrackingType,
    Guid BaseUnitId,
    Guid? ConversionId = null,
    Guid? ConversionFromUnitId = null,
    Guid? ConversionToUnitId = null,
    decimal? ConversionFactor = null)
{
    /// <summary>True when the line was written against a unit conversion rather than the base unit.</summary>
    public bool HasConversion => ConversionId is not null;

    /// <summary>
    /// Builds a snapshot, failing closed when the capture is incomplete: a non-positive catalog
    /// revision, a missing base unit, a conversion identity without its units/factor, or a factor
    /// that cannot produce a base quantity. Callers must handle the failure; there is no default
    /// value to fall back to.
    /// </summary>
    public static Result<DocumentLineProvenance> Create(
        int materialVersion,
        MaterialKind materialKind,
        TrackingType trackingType,
        Guid baseUnitId,
        Guid? conversionId = null,
        Guid? conversionFromUnitId = null,
        Guid? conversionToUnitId = null,
        decimal? conversionFactor = null)
    {
        Result validation = Validate(
            materialVersion,
            baseUnitId,
            conversionId,
            conversionFromUnitId,
            conversionToUnitId,
            conversionFactor);

        if (validation.IsFailure)
        {
            return Result.Failure<DocumentLineProvenance>(validation.Error);
        }

        return new DocumentLineProvenance(
            materialVersion,
            materialKind,
            trackingType,
            baseUnitId,
            conversionId,
            conversionFromUnitId,
            conversionToUnitId,
            conversionFactor);
    }

    /// <summary>
    /// Re-checks a snapshot, including one built through the primary constructor, so a caller can
    /// never store a capture the domain would refuse to build.
    /// </summary>
    public Result Validate() => Validate(
        MaterialVersion,
        BaseUnitId,
        ConversionId,
        ConversionFromUnitId,
        ConversionToUnitId,
        ConversionFactor);

    private static Result Validate(
        int materialVersion,
        Guid baseUnitId,
        Guid? conversionId,
        Guid? conversionFromUnitId,
        Guid? conversionToUnitId,
        decimal? conversionFactor)
    {
        if (materialVersion <= 0)
        {
            return Result.Failure(DocumentLineErrors.ProvenanceCaptureInvalid(
                "material_version",
                $"must be greater than zero but was {materialVersion}"));
        }

        if (baseUnitId == Guid.Empty)
        {
            return Result.Failure(DocumentLineErrors.ProvenanceCaptureInvalid(
                "base_unit_id",
                "must be the material's base unit but was empty"));
        }

        bool conversionFieldsPresent = conversionId is not null ||
            conversionFromUnitId is not null ||
            conversionToUnitId is not null ||
            conversionFactor is not null;

        if (!conversionFieldsPresent)
        {
            return Result.Success();
        }

        if (conversionId is null || conversionFromUnitId is null || conversionToUnitId is null)
        {
            return Result.Failure(DocumentLineErrors.ProvenanceCaptureInvalid(
                "conversion",
                "requires the conversion identity together with its from/to units"));
        }

        if (conversionFactor is null || conversionFactor <= 0m)
        {
            return Result.Failure(DocumentLineErrors.ProvenanceCaptureInvalid(
                "conversion_factor",
                "must be greater than zero when a conversion is captured"));
        }

        return Result.Success();
    }
}
