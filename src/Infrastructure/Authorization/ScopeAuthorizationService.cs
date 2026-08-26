using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Infrastructure.Authorization;

internal sealed class ScopeAuthorizationService(
    IApplicationDbContext context,
    HybridCache hybridCache) : IScopeAuthorizationService
{
    public async Task<bool> HasPermissionInScopeAsync(
        Guid userId,
        string permission,
        ScopeType scopeType,
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        List<UserPermissionScopeGrant> allGrants = await GetAllGrantsAsync(userId, cancellationToken);

        var grants = allGrants
            .Where(g => g.PermissionCode == permission)
            .ToList();

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
            Guid? warehouseSiteId = await hybridCache.GetOrCreateAsync(
                $"warehouse:site-id:{scopeId}",
                async ct => await context.Warehouses
                    .AsNoTracking()
                    .Where(w => w.Id == scopeId)
                    .Select(w => (Guid?)w.SiteId)
                    .SingleOrDefaultAsync(ct),
                new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(30),
                    LocalCacheExpiration = TimeSpan.FromMinutes(30)
                },
                tags: ["warehouses"],
                cancellationToken: cancellationToken);

            if (warehouseSiteId is not null &&
                grants.Any(grant => grant.ScopeType == ScopeType.Site && grant.ScopeId == warehouseSiteId))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<WarehousePermissionScope> GetWarehousePermissionScopeAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        List<UserPermissionScopeGrant> allGrants = await GetAllGrantsAsync(userId, cancellationToken);
        var grants = allGrants
            .Where(grant => grant.PermissionCode == permission)
            .ToList();

        if (grants.Any(grant => grant.ScopeType == ScopeType.Enterprise))
        {
            return new WarehousePermissionScope(true, new HashSet<Guid>());
        }

        Guid[] siteIds = grants
            .Where(grant => grant.ScopeType == ScopeType.Site && grant.ScopeId.HasValue)
            .Select(grant => grant.ScopeId!.Value)
            .Distinct()
            .ToArray();

        Guid[] siteWarehouseIds = siteIds.Length == 0
            ? []
            : await context.Warehouses
                .AsNoTracking()
                .Where(warehouse => siteIds.Contains(warehouse.SiteId))
                .Select(warehouse => warehouse.Id)
                .ToArrayAsync(cancellationToken);

        var warehouseIds = grants
            .Where(grant => grant.ScopeType == ScopeType.Warehouse && grant.ScopeId.HasValue)
            .Select(grant => grant.ScopeId!.Value)
            .Concat(siteWarehouseIds)
            .ToHashSet();

        return new WarehousePermissionScope(false, warehouseIds);
    }

    private async Task<List<UserPermissionScopeGrant>> GetAllGrantsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await hybridCache.GetOrCreateAsync(
            $"auth:user-grants:{userId}",
            async ct => await (
                from userRoleScope in context.UserRoleScopes.AsNoTracking()
                where userRoleScope.UserId == userId
                join rolePermission in context.RolePermissions.AsNoTracking()
                    on userRoleScope.RoleId equals rolePermission.RoleId
                join grantedPermission in context.Permissions.AsNoTracking()
                    on rolePermission.PermissionId equals grantedPermission.Id
                select new UserPermissionScopeGrant(
                    grantedPermission.Code,
                    userRoleScope.ScopeType,
                    userRoleScope.ScopeId))
                .Distinct()
                .ToListAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: [$"user:{userId}", "auth-roles"],
            cancellationToken: cancellationToken);

    public sealed record UserPermissionScopeGrant(string PermissionCode, ScopeType ScopeType, Guid? ScopeId);
}
