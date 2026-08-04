namespace Application.MaterialCategories.GetList;

public sealed class MaterialCategoryResponse
{
    public Guid Id { get; init; }

    public Guid MaterialDomainId { get; init; }

    public Guid? ParentCategoryId { get; init; }

    public string Name { get; init; }

    public string Code { get; init; }

    public string Status { get; init; }
}
