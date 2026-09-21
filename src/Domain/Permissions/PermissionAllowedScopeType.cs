using Domain.Common;

namespace Domain.Permissions;

/// <summary>
/// Explicitly limits a permission to the scope types where it may be effective.
/// This is independent of the scope types to which a role may be assigned.
/// </summary>
public sealed class PermissionAllowedScopeType
{
    private PermissionAllowedScopeType() { }

    public Guid PermissionId { get; private set; }
    public ScopeType ScopeType { get; private set; }

    public static PermissionAllowedScopeType Create(Guid permissionId, ScopeType scopeType) => new()
    {
        PermissionId = permissionId,
        ScopeType = scopeType
    };
}
