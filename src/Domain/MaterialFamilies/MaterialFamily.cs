using Domain.Common;
using SharedKernel;

namespace Domain.MaterialFamilies;

/// <summary>
/// Mandatory grouping family (fourth catalog level). Quantity semantics belong to Material.
/// </summary>
public sealed class MaterialFamily : Entity, IAuditableEntity
{
    private MaterialFamily() { }

    public Guid CategoryId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static MaterialFamily Create(Guid id, Guid categoryId, string name, string code)
    {
        var family = new MaterialFamily
        {
            Id = id,
            CategoryId = categoryId,
            Name = name,
            Code = code,
            Status = Status.Active
        };

        family.Raise(new MaterialFamilyCreatedDomainEvent(family.Id, family.CategoryId));

        return family;
    }

    public void UpdateDetails(string name, string code)
    {
        Name = name;
        Code = code;
        Raise(new MaterialFamilyUpdatedDomainEvent(Id));
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;

        Raise(new MaterialFamilyStatusChangedDomainEvent(Id, status));
    }
}
