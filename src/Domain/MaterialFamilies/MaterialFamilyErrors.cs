using SharedKernel;

namespace Domain.MaterialFamilies;

public static class MaterialFamilyErrors
{
    public static Error NotFound(Guid familyId) => Error.NotFound(
        "MaterialFamilies.NotFound",
        $"The material family with the Id = '{familyId}' was not found");

    public static Error CategoryNotFound(Guid categoryId) => Error.NotFound(
        "MaterialFamilies.CategoryNotFound",
        $"The material category with the Id = '{categoryId}' was not found");

    public static Error BaseUnitNotFound(Guid baseUnitId) => Error.NotFound(
        "MaterialFamilies.BaseUnitNotFound",
        $"The unit of measure with the Id = '{baseUnitId}' was not found");

    public static readonly Error Forbidden = Error.Forbidden(
        "MaterialFamilies.Forbidden",
        "You are not authorized to manage material families.");
}
