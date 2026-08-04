namespace Application.Organizations.GetById;

public sealed class OrganizationResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string Code { get; init; }

    public string Status { get; init; }
}
