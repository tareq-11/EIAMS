using Domain.DocumentLines;
using Domain.MaterialUnitConversions;
using SharedKernel;

namespace Application.DocumentLines;

/// <summary>
/// Computes the authoritative <c>BaseQuantity</c> for a document line from the client-supplied
/// <c>Quantity</c>/<c>UnitId</c> (M3-PLAN.md §1.5). Pure calculation - callers resolve the family's
/// base unit and the applicable <see cref="MaterialUnitConversion"/> (if any) from the database
/// first, since this class has no persistence access of its own.
/// </summary>
public static class BaseQuantityCalculator
{
    /// <summary>Largest magnitude that fits in decimal(18,3) - 15 integer digits, 3 fractional.</summary>
    private const decimal MaxBaseQuantity = 999_999_999_999_999.999m;

    public static Result<decimal> Calculate(
        Guid materialId,
        decimal quantity,
        Guid? unitId,
        Guid familyBaseUnitId,
        MaterialUnitConversion? conversion)
    {
        if (quantity > MaxBaseQuantity || decimal.Round(quantity, 3) != quantity)
        {
            return Result.Failure<decimal>(DocumentLineErrors.QuantityPrecisionInvalid);
        }

        if (unitId is null || unitId == familyBaseUnitId)
        {
            return quantity;
        }

        if (conversion is null)
        {
            return Result.Failure<decimal>(DocumentLineErrors.UnitConversionNotFound(materialId, unitId.Value));
        }

        decimal baseQuantity;

        try
        {
            baseQuantity = decimal.Round(checked(quantity * conversion.Factor), 3, MidpointRounding.AwayFromZero);
        }
        catch (OverflowException)
        {
            return Result.Failure<decimal>(DocumentLineErrors.BaseQuantityOverflow);
        }

        if (baseQuantity <= 0 || baseQuantity > MaxBaseQuantity)
        {
            return Result.Failure<decimal>(DocumentLineErrors.BaseQuantityOverflow);
        }

        return baseQuantity;
    }
}
