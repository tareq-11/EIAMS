using SharedKernel;

namespace Domain.UnitsOfMeasure;

public static class UnitOfMeasureErrors
{
    public static Error NotFound(Guid unitId) => Error.NotFound(
        "UnitsOfMeasure.NotFound",
        $"The unit of measure with the Id = '{unitId}' was not found");

    public static readonly Error Forbidden = Error.Forbidden(
        "UnitsOfMeasure.Forbidden",
        "You are not authorized to manage units of measure.");
}
