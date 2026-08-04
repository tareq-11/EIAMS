using SharedKernel;

namespace Domain.Permissions;

/// <summary>
/// An atomic permission code. Permissions are compiled-in reference data (seeded via migration),
/// not user-manageable at runtime - new codes are added by developers as features gain endpoints.
/// </summary>
public sealed class Permission : Entity
{
    private Permission() { }

    public string Code { get; private set; }
    public string? Description { get; private set; }

    public static Permission Create(Guid id, string code, string? description)
    {
        return new Permission
        {
            Id = id,
            Code = code,
            Description = description
        };
    }
}
