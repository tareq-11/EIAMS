namespace Domain.Roles;

/// <summary>
/// Grants a Permission to a Role. Composite key (RoleId, PermissionId) - a pure join row with no
/// independent identity, mutated directly by the assign/remove handlers.
/// </summary>
public sealed class RolePermission
{
    private RolePermission() { }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    public static RolePermission Create(Guid roleId, Guid permissionId)
    {
        return new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId
        };
    }
}
