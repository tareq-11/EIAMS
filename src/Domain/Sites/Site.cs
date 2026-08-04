using Domain.Common;
using SharedKernel;

namespace Domain.Sites;

public sealed class Site : Entity, IAuditableEntity
{
    private Site() { }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Location { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Site Create(Guid id, Guid organizationId, string name, string code, string? location)
    {
        var site = new Site
        {
            Id = id,
            OrganizationId = organizationId,
            Name = name,
            Code = code,
            Location = location,
            Status = Status.Active
        };

        site.Raise(new SiteCreatedDomainEvent(site.Id, site.OrganizationId));

        return site;
    }

    public void UpdateDetails(string name, string? location)
    {
        Name = name;
        Location = location;
        Raise(new SiteUpdatedDomainEvent(Id));
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;

        Raise(new SiteStatusChangedDomainEvent(Id, status));
    }
}
