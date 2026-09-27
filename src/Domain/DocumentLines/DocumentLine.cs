using Domain.Common;
using Domain.Materials;
using SharedKernel;

namespace Domain.DocumentLines;

/// <summary>
/// One material line on a WarehouseDocument. <see cref="BaseQuantity"/> is authoritative and always
/// server-computed (see Application's base-quantity calculation, M3-PLAN.md §1.5) - clients send
/// only <see cref="Quantity"/> and optionally <see cref="UnitId"/>. Only ever mutated while the
/// owning document is Draft; that guard lives in the Application handler, not here, since this
/// entity has no reference back to its document's status (shadow-FK style, per this codebase's
/// convention). The line also freezes the catalog state it was written against
/// (<see cref="Provenance"/>), which is re-validated at submit and post so a later catalog edit
/// cannot reinterpret the document.
/// </summary>
public sealed class DocumentLine : Entity, IAuditableEntity
{
    private const decimal MaxQuantity = 999_999_999_999_999.999m;
    private const decimal MaxUnitPrice = 9_999_999_999_999_999.99m;

    private DocumentLine() { }

    public Guid DocumentId { get; private set; }
    public Guid? SourceLineId { get; private set; }
    public Guid MaterialId { get; private set; }
    public DocumentLineType LineType { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid? UnitId { get; private set; }
    public decimal BaseQuantity { get; private set; }

    /// <summary>
    /// Revision of the source material's classification this line was captured against
    /// (see <see cref="DocumentLineProvenance"/>). Null only for lines created before provenance
    /// capture existed; those are validated against the live catalog instead.
    /// </summary>
    public int? SourceMaterialVersion { get; private set; }

    /// <summary>Source material classification captured with the line, never re-derived on read.</summary>
    public MaterialKind? SourceMaterialKind { get; private set; }

    /// <summary>Source material tracking type captured with the line, never re-derived on read.</summary>
    public TrackingType? SourceTrackingType { get; private set; }

    /// <summary>The material's base unit (<c>Material.BaseUnitId</c>) captured with the line.</summary>
    public Guid? SourceBaseUnitId { get; private set; }

    /// <summary>Identity of the conversion that produced <see cref="BaseQuantity"/>, when one was used.</summary>
    public Guid? SourceConversionId { get; private set; }

    public Guid? SourceConversionFromUnitId { get; private set; }

    public Guid? SourceConversionToUnitId { get; private set; }

    public decimal? SourceConversionFactor { get; private set; }

    public decimal? UnitPrice { get; private set; }
    public string? BatchNumber { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public OpeningType? OpeningType { get; private set; }

    /// <summary>
    /// The captured catalog snapshot, or null when the line has none.
    /// <para>
    /// A line that predates provenance capture has every snapshot column null and is reported as
    /// "never captured" by <see cref="RequireProvenance"/>. A row that records a catalog revision
    /// without the rest of the snapshot is corrupt: it is reported as null here as well, but
    /// <see cref="RequireProvenance"/> distinguishes it. No field is ever defaulted - a missing
    /// classification, tracking type or base unit is never reconstructed with a substitute value,
    /// because a fabricated base unit would silently reinterpret the line's quantity.
    /// </para>
    /// </summary>
    public DocumentLineProvenance? Provenance => SourceMaterialVersion is null || !HasCompleteProvenance
        ? null
        : new DocumentLineProvenance(
            SourceMaterialVersion.Value,
            SourceMaterialKind!.Value,
            SourceTrackingType!.Value,
            SourceBaseUnitId!.Value,
            SourceConversionId,
            SourceConversionFromUnitId,
            SourceConversionToUnitId,
            SourceConversionFactor);

    /// <summary>True when a catalog revision is stored together with the rest of the snapshot.</summary>
    public bool HasCompleteProvenance =>
        SourceMaterialVersion is not null &&
        SourceMaterialKind is not null &&
        SourceTrackingType is not null &&
        SourceBaseUnitId is not null &&
        SourceBaseUnitId != Guid.Empty;

    /// <summary>
    /// The captured snapshot, failing closed when it is missing or partial. This is what a submit or
    /// post transition must call: an unverifiable line blocks the transition instead of being
    /// validated against whatever the catalog happens to say now.
    /// </summary>
    public Result<DocumentLineProvenance> RequireProvenance()
    {
        if (Provenance is { } captured)
        {
            return Result.Success(captured);
        }

        return Result.Failure<DocumentLineProvenance>(SourceMaterialVersion is null
            ? DocumentLineErrors.ProvenanceNotCaptured(DocumentId, Id)
            : DocumentLineErrors.ProvenanceIncomplete(DocumentId, Id, SourceMaterialVersion));
    }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<DocumentLine> Create(
        Guid id,
        Guid documentId,
        Guid materialId,
        DocumentLineType lineType,
        decimal quantity,
        Guid? unitId,
        decimal baseQuantity,
        decimal? unitPrice,
        string? batchNumber,
        DateOnly? expiryDate,
        OpeningType? openingType = null,
        Guid? sourceLineId = null,
        DocumentLineProvenance? provenance = null)
    {
        Result validation = Validate(quantity, baseQuantity, unitPrice);

        if (validation.IsFailure)
        {
            return Result.Failure<DocumentLine>(validation.Error);
        }

        Result capture = ValidateProvenance(provenance);

        if (capture.IsFailure)
        {
            return Result.Failure<DocumentLine>(capture.Error);
        }

        var line = new DocumentLine
        {
            Id = id,
            DocumentId = documentId,
            SourceLineId = sourceLineId,
            MaterialId = materialId,
            LineType = lineType,
            Quantity = quantity,
            UnitId = unitId,
            BaseQuantity = baseQuantity,
            UnitPrice = unitPrice,
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            OpeningType = openingType
        };

        line.CaptureProvenance(provenance);

        line.Raise(new DocumentLineAddedDomainEvent(line.Id, documentId, materialId));

        return line;
    }

    public Result Update(
        DocumentLineType lineType,
        decimal quantity,
        Guid? unitId,
        decimal baseQuantity,
        decimal? unitPrice,
        string? batchNumber,
        DateOnly? expiryDate,
        OpeningType? openingType,
        DocumentLineProvenance? provenance = null)
    {
        Result validation = Validate(quantity, baseQuantity, unitPrice);

        if (validation.IsFailure)
        {
            return validation;
        }

        Result capture = ValidateProvenance(provenance);

        if (capture.IsFailure)
        {
            return capture;
        }

        LineType = lineType;
        Quantity = quantity;
        UnitId = unitId;
        BaseQuantity = baseQuantity;
        UnitPrice = unitPrice;
        BatchNumber = batchNumber;
        ExpiryDate = expiryDate;
        OpeningType = openingType;

        CaptureProvenance(provenance);

        Raise(new DocumentLineUpdatedDomainEvent(Id, DocumentId));

        return Result.Success();
    }

    public void MarkAsRemoved()
    {
        Raise(new DocumentLineRemovedDomainEvent(Id, DocumentId));
    }

    private static Result ValidateProvenance(DocumentLineProvenance? provenance) =>
        provenance?.Validate() ?? Result.Success();

    private void CaptureProvenance(DocumentLineProvenance? provenance)
    {
        if (provenance is null)
        {
            return;
        }

        SourceMaterialVersion = provenance.MaterialVersion;
        SourceMaterialKind = provenance.MaterialKind;
        SourceTrackingType = provenance.TrackingType;
        SourceBaseUnitId = provenance.BaseUnitId;
        SourceConversionId = provenance.ConversionId;
        SourceConversionFromUnitId = provenance.ConversionFromUnitId;
        SourceConversionToUnitId = provenance.ConversionToUnitId;
        SourceConversionFactor = provenance.ConversionFactor;
    }

    private static Result Validate(decimal quantity, decimal baseQuantity, decimal? unitPrice)
    {
        if (quantity <= 0)
        {
            return Result.Failure(DocumentLineErrors.QuantityMustBePositive);
        }

        if (quantity > MaxQuantity || decimal.Round(quantity, 3) != quantity)
        {
            return Result.Failure(DocumentLineErrors.QuantityPrecisionInvalid);
        }

        if (baseQuantity <= 0)
        {
            return Result.Failure(DocumentLineErrors.BaseQuantityMustBePositive);
        }

        if (baseQuantity > MaxQuantity || decimal.Round(baseQuantity, 3) != baseQuantity)
        {
            return Result.Failure(DocumentLineErrors.BaseQuantityOverflow);
        }

        if (unitPrice is < 0)
        {
            return Result.Failure(DocumentLineErrors.UnitPriceMustBeNonNegative);
        }

        if (unitPrice is not null &&
            (unitPrice > MaxUnitPrice || decimal.Round(unitPrice.Value, 2) != unitPrice.Value))
        {
            return Result.Failure(DocumentLineErrors.UnitPricePrecisionInvalid);
        }

        return Result.Success();
    }
}
