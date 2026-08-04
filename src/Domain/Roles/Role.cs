using SharedKernel;

namespace Domain.Roles;

public sealed class Role : Entity, IAuditableEntity
{
    private Role() { }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Role Create(Guid id, string name, string? description)
    {
        var role = new Role
        {
            Id = id,
            Name = name,
            Description = description
        };

        role.Raise(new RoleCreatedDomainEvent(role.Id));

        return role;
    }

    public void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
    }
}
