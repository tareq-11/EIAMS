using SharedKernel;

namespace Domain.MaterialUnitConversions;

public static class MaterialUnitConversionErrors
{
    public static Error NotFound(Guid conversionId) => Error.NotFound(
        "MaterialUnitConversions.NotFound",
        $"The material unit conversion with the Id = '{conversionId}' was not found");

    public static Error MaterialNotFound(Guid materialId) => Error.NotFound(
        "MaterialUnitConversions.MaterialNotFound",
        $"The material with the Id = '{materialId}' was not found");

    public static Error UnitNotFound(Guid unitId) => Error.NotFound(
        "MaterialUnitConversions.UnitNotFound",
        $"The unit of measure with the Id = '{unitId}' was not found");

    public static readonly Error BaseUnitMismatch = Error.Problem(
        "MaterialUnitConversions.BaseUnitMismatch",
        "The target unit must be the material family's base unit");

    public static readonly Error SameUnit = Error.Problem(
        "MaterialUnitConversions.SameUnit",
        "The source unit must be different from the base unit");

    public static readonly Error UnitTypeMismatch = Error.Problem(
        "MaterialUnitConversions.UnitTypeMismatch",
        "The source and base units must have the same unit type");

    public static readonly Error AlreadyExists = Error.Conflict(
        "MaterialUnitConversions.AlreadyExists",
        "A conversion from this unit already exists for the material");

    public static readonly Error Forbidden = Error.Forbidden(
        "MaterialUnitConversions.Forbidden",
        "You are not authorized to manage material unit conversions.");
}
