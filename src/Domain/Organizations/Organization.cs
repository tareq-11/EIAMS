using Domain.Common;
using SharedKernel;

namespace Domain.Organizations;

public sealed class Organization : Entity, IAuditableEntity
{
    private Organization() { }

    public string Name { get; private set; }
    public string Code { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Organization Create(Guid id, string name, string code)
    {
        var organization = new Organization
        {
            Id = id,
            Name = name,
            Code = code,
            Status = Status.Active
        };

        organization.Raise(new OrganizationCreatedDomainEvent(organization.Id));

        return organization;
    }

    public void UpdateDetails(string name)
    {
        Name = name;
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;

        Raise(new OrganizationStatusChangedDomainEvent(Id, status));
    }
}
