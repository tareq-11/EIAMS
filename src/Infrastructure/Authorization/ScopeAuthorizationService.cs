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

        if (grants.Count == 0)
        {
            return false;
        }

        if (grants.Any(grant => grant.ScopeType == ScopeType.Enterprise))
        {
            // Enterprise is org-wide - it satisfies every scoped request.
            return true;
        }

        if (grants.Any(grant => grant.ScopeType == scopeType && grant.ScopeId == scopeId))
        {
            return true;
        }

        if (scopeType == ScopeType.Warehouse && scopeId is not null)
        {
            // A Site-scoped grant also covers every Warehouse within that site.
            Guid? warehouseSiteId = await context.Warehouses
                .Where(w => w.Id == scopeId)
                .Select(w => (Guid?)w.SiteId)
                .SingleOrDefaultAsync(cancellationToken);

            if (warehouseSiteId is not null &&
                grants.Any(grant => grant.ScopeType == ScopeType.Site && grant.ScopeId == warehouseSiteId))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record ScopeGrant(ScopeType ScopeType, Guid? ScopeId);
}
