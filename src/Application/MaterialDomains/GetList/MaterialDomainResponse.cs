namespace Application.MaterialDomains.GetList;

public sealed class MaterialDomainResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string Code { get; init; }

    public string Status { get; init; }
}
