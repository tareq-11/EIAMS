using Domain.Common;
using SharedKernel;

namespace Domain.MaterialCategories;

public sealed class MaterialCategory : Entity, IAuditableEntity
{
    private MaterialCategory() { }

    public Guid MaterialDomainId { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static MaterialCategory Create(Guid id, Guid materialDomainId, Guid? parentCategoryId, string name, string code)
    {
        var category = new MaterialCategory
        {
            Id = id,
            MaterialDomainId = materialDomainId,
            ParentCategoryId = parentCategoryId,
            Name = name,
            Code = code,
            Status = Status.Active
        };

        category.Raise(new MaterialCategoryCreatedDomainEvent(category.Id, category.MaterialDomainId));

        return category;
    }

    public void UpdateDetails(string name, string code)
    {
        Name = name;
        Code = code;
        Raise(new MaterialCategoryUpdatedDomainEvent(Id));
    }

    public void MoveTo(Guid? parentCategoryId)
    {
        if (ParentCategoryId == parentCategoryId)
        {
            return;
        }

        ParentCategoryId = parentCategoryId;
        Raise(new MaterialCategoryMovedDomainEvent(Id, parentCategoryId));
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;

        Raise(new MaterialCategoryStatusChangedDomainEvent(Id, status));
    }
}
