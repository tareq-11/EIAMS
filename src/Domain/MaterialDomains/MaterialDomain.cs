using Domain.Common;
using SharedKernel;

namespace Domain.MaterialDomains;

/// <summary>
/// Top-level material domain (PRD Ch. 3.2). Drives which warehouses can handle a material via
/// WarehouseCapability (M2) - materials become available to a warehouse by domain, never by a
/// direct binding.
/// </summary>
public sealed class MaterialDomain : Entity, IAuditableEntity
{
    private MaterialDomain() { }

    public string Name { get; private set; }
    public string Code { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static MaterialDomain Create(Guid id, string name, string code)
    {
        var materialDomain = new MaterialDomain
        {
            Id = id,
            Name = name,
            Code = code,
            Status = Status.Active
        };

        materialDomain.Raise(new MaterialDomainCreatedDomainEvent(materialDomain.Id));

        return materialDomain;
    }

    public void UpdateDetails(string name)
    {
        Name = name;
        Raise(new MaterialDomainUpdatedDomainEvent(Id));
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;

        Raise(new MaterialDomainStatusChangedDomainEvent(Id, status));
    }
}
