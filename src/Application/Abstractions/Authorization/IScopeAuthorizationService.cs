using Domain.Common;

namespace Application.Abstractions.Authorization;

/// <summary>
/// Resolves whether a user's role grants (<c>UserRoleScope</c>) satisfy a permission for a specific
/// scope. An Enterprise-scoped grant satisfies any request; a Site/Warehouse-scoped grant satisfies
/// only requests for that exact scope (see <c>ScopeAuthorizationService</c> for the hierarchy rules).
/// This is a finer-grained, resource-aware check layered on top of the coarse-grained
/// <c>HasPermission</c> endpoint gate, which only checks whether the user holds the permission
/// in any scope at all.
/// </summary>
public interface IScopeAuthorizationService
{
    Task<bool> HasPermissionInScopeAsync(
        Guid userId,
        string permission,
        ScopeType scopeType,
        Guid? scopeId,
        CancellationToken cancellationToken);
}
