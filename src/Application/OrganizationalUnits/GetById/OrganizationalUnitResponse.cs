namespace Application.OrganizationalUnits.GetById;

public sealed class OrganizationalUnitResponse
{
    public Guid Id { get; init; }

    public Guid SiteId { get; init; }

    public Guid? ParentId { get; init; }

    public string Name { get; init; }

    public string UnitType { get; init; }

    public string Status { get; init; }
}
