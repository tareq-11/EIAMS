namespace Application.MaterialFamilies.GetById;

public sealed class MaterialFamilyResponse
{
    public Guid Id { get; init; }

    public Guid CategoryId { get; init; }

    public string Name { get; init; }

    public string Code { get; init; }

    public Guid BaseUnitId { get; init; }

    public string Status { get; init; }
}
