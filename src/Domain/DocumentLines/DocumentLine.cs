using Domain.Common;
using SharedKernel;

namespace Domain.DocumentLines;

/// <summary>
/// One material line on a WarehouseDocument. <see cref="BaseQuantity"/> is authoritative and always
/// server-computed (see Application's base-quantity calculation, M3-PLAN.md §1.5) - clients send
/// only <see cref="Quantity"/> and optionally <see cref="UnitId"/>. Only ever mutated while the
/// owning document is Draft; that guard lives in the Application handler, not here, since this
/// entity has no reference back to its document's status (shadow-FK style, per this codebase's
/// convention).
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
    public decimal? UnitPrice { get; private set; }
    public string? BatchNumber { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }

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
        Guid? sourceLineId = null)
    {
        Result validation = Validate(quantity, baseQuantity, unitPrice);

        if (validation.IsFailure)
        {
            return Result.Failure<DocumentLine>(validation.Error);
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
            ExpiryDate = expiryDate
        };

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
        DateOnly? expiryDate)
    {
        Result validation = Validate(quantity, baseQuantity, unitPrice);

        if (validation.IsFailure)
        {
            return validation;
        }

        LineType = lineType;
        Quantity = quantity;
        UnitId = unitId;
        BaseQuantity = baseQuantity;
        UnitPrice = unitPrice;
        BatchNumber = batchNumber;
        ExpiryDate = expiryDate;

        Raise(new DocumentLineUpdatedDomainEvent(Id, DocumentId));

        return Result.Success();
    }

    public void MarkAsRemoved()
    {
        Raise(new DocumentLineRemovedDomainEvent(Id, DocumentId));
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
