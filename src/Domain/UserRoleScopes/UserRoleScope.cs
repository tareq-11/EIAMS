using Domain.Common;
using SharedKernel;

namespace Domain.UserRoleScopes;

/// <summary>
/// The user's single role and fixed authorization scope. ScopeId is null for Enterprise and required
/// otherwise. There is no status column: replacement mutates this one row atomically and revocation
/// deletes it, while the immutable audit log preserves history.
/// </summary>
public sealed class UserRoleScope : Entity, IAuditableEntity
{
    private UserRoleScope() { }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public ScopeType ScopeType { get; private set; }
    public Guid? ScopeId { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static UserRoleScope Create(Guid id, Guid userId, Guid roleId, ScopeType scopeType, Guid? scopeId)
    {
        var userRoleScope = new UserRoleScope
        {
            Id = id,
            UserId = userId,
            RoleId = roleId,
            ScopeType = scopeType,
            ScopeId = scopeId
        };

        userRoleScope.Raise(new UserRoleScopeGrantedDomainEvent(userRoleScope.Id, userId, roleId, scopeType, scopeId));

        return userRoleScope;
    }

    public void MarkAsRevoked()
    {
        Raise(new UserRoleScopeRevokedDomainEvent(Id, UserId, RoleId));
    }

    public void ReplaceAssignment(Guid roleId, ScopeType scopeType, Guid? scopeId)
    {
        Guid previousRoleId = RoleId;
        ScopeType previousScopeType = ScopeType;
        Guid? previousScopeId = ScopeId;

        RoleId = roleId;
        ScopeType = scopeType;
        ScopeId = scopeId;

        Raise(new UserRoleScopeReplacedDomainEvent(
            Id,
            UserId,
            previousRoleId,
            previousScopeType,
            previousScopeId,
            roleId,
            scopeType,
            scopeId));
    }
}
