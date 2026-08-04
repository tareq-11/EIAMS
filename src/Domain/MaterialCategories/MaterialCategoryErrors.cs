using SharedKernel;

namespace Domain.MaterialCategories;

public static class MaterialCategoryErrors
{
    public static Error NotFound(Guid categoryId) => Error.NotFound(
        "MaterialCategories.NotFound",
        $"The material category with the Id = '{categoryId}' was not found");

    public static Error MaterialDomainNotFound(Guid materialDomainId) => Error.NotFound(
        "MaterialCategories.MaterialDomainNotFound",
        $"The material domain with the Id = '{materialDomainId}' was not found");

    public static Error ParentNotFound(Guid parentCategoryId) => Error.NotFound(
        "MaterialCategories.ParentNotFound",
        $"The parent category with the Id = '{parentCategoryId}' was not found");

    public static Error ParentInDifferentDomain(Guid parentCategoryId) => Error.Problem(
        "MaterialCategories.ParentInDifferentDomain",
        $"The parent category with the Id = '{parentCategoryId}' belongs to a different material domain");

    public static readonly Error CircularParent = Error.Conflict(
        "MaterialCategories.CircularParent",
        "Moving the category under the selected parent would create a circular category tree");

    public static readonly Error Forbidden = Error.Forbidden(
        "MaterialCategories.Forbidden",
        "You are not authorized to manage material categories.");
}
