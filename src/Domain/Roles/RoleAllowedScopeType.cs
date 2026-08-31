using Domain.Common;

namespace Domain.Roles;

/// <summary>
/// Declares the authorization scope levels at which a role may be assigned.
/// </summary>
public sealed class RoleAllowedScopeType
{
    private RoleAllowedScopeType() { }

    public Guid RoleId { get; private set; }
    public ScopeType ScopeType { get; private set; }

    public static RoleAllowedScopeType Create(Guid roleId, ScopeType scopeType) => new()
    {
        RoleId = roleId,
        ScopeType = scopeType
    };
}
