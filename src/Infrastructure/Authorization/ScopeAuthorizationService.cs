using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authorization;

internal sealed class ScopeAuthorizationService(IApplicationDbContext context) : IScopeAuthorizationService
{
    public async Task<bool> HasPermissionInScopeAsync(
        Guid userId,
        string permission,
        ScopeType scopeType,
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        List<ScopeGrant> grants = await (
                from userRoleScope in context.UserRoleScopes
                where userRoleScope.UserId == userId
                join rolePermission in context.RolePermissions
                    on userRoleScope.RoleId equals rolePermission.RoleId
                join grantedPermission in context.Permissions
                    on rolePermission.PermissionId equals grantedPermission.Id
                where grantedPermission.Code == permission
                select new ScopeGrant(userRoleScope.ScopeType, userRoleScope.ScopeId))
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (ScopeGrant grant in grants)
        {
            if (grant.ScopeType == ScopeType.Enterprise)
            {
                // Enterprise is org-wide - it satisfies every scoped request.
                return true;
            }

            if (grant.ScopeType == scopeType && grant.ScopeId == scopeId)
            {
                return true;
            }

            // NOTE: A Site-scoped grant should also cover every Warehouse within that site, but
            // resolving a warehouse's owning site requires the Warehouse entity introduced in M2.
            // Until then, Warehouse-scoped requests only succeed via an exact Warehouse-scoped or
            // an Enterprise grant. Extend this branch once Warehouse.SiteId exists.
        }

        return false;
    }

    private sealed record ScopeGrant(ScopeType ScopeType, Guid? ScopeId);
}
