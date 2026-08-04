using Domain.Common;
using SharedKernel;

namespace Domain.OrganizationalUnits;

public sealed class OrganizationalUnit : Entity, IAuditableEntity
{
    private OrganizationalUnit() { }

    public Guid SiteId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; }
    public string UnitType { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static OrganizationalUnit Create(Guid id, Guid siteId, Guid? parentId, string name, string unitType)
    {
        var unit = new OrganizationalUnit
        {
            Id = id,
            SiteId = siteId,
            ParentId = parentId,
            Name = name,
            UnitType = unitType,
            Status = Status.Active
        };

        unit.Raise(new OrganizationalUnitCreatedDomainEvent(unit.Id, unit.SiteId));

        return unit;
    }

    public void UpdateDetails(string name, string unitType)
    {
        Name = name;
        UnitType = unitType;
        Raise(new OrganizationalUnitUpdatedDomainEvent(Id));
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;

        Raise(new OrganizationalUnitStatusChangedDomainEvent(Id, status));
    }
}
