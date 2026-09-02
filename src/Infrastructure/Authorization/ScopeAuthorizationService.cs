using Application.Abstractions.Authorization;
using Domain.Common;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Infrastructure.Authorization;

internal sealed class ScopeAuthorizationService(
    ApplicationDbContext context,
    HybridCache hybridCache) : IScopeAuthorizationService
{
    public async Task<UserAuthorizationAssignment?> GetUserAssignmentAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await GetOrCreateAsync(
            $"auth:user-assignment:{userId}",
            "user_assignment",
            async ct => await context.UserRoleScopes
                .AsNoTracking()
                .Where(assignment => assignment.UserId == userId)
                .Select(assignment => new UserAuthorizationAssignment(
                    assignment.Id,
                    assignment.UserId,
                    assignment.RoleId,
                    assignment.ScopeType,
                    assignment.ScopeId))
                .SingleOrDefaultAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: [$"user:{userId}", "auth-roles"],
            cancellationToken: cancellationToken);

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        List<UserPermissionScopeGrant> grants = await GetAllGrantsAsync(userId, cancellationToken);
        return grants.Any(grant => grant.PermissionCode == permission);
    }

    public async Task<bool> HasPermissionInScopeAsync(
        Guid userId,
        string permission,
        ScopeType scopeType,
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        UserPermissionScopeGrant? grant = await GetGrantAsync(userId, permission, cancellationToken);

        return grant is not null && await AssignmentContainsAsync(
            grant.ScopeType,
            grant.ScopeId,
            scopeType,
            scopeId,
            cancellationToken);
    }

    public async Task<bool> CanAccessSiteAsync(
        Guid userId,
        Guid siteId,
        CancellationToken cancellationToken)
    {
        UserAuthorizationAssignment? assignment = await GetUserAssignmentAsync(userId, cancellationToken);

        if (assignment is null)
        {
            return false;
        }

        return assignment.ScopeType switch
        {
            ScopeType.Enterprise => true,
            ScopeType.Site => assignment.ScopeId == siteId,
            ScopeType.OrganizationalUnit => await context.OrganizationalUnits
                .AsNoTracking()
                .AnyAsync(unit => unit.Id == assignment.ScopeId && unit.SiteId == siteId, cancellationToken),
            ScopeType.Warehouse => await context.Warehouses
                .AsNoTracking()
                .AnyAsync(warehouse => warehouse.Id == assignment.ScopeId && warehouse.SiteId == siteId, cancellationToken),
            _ => false
        };
    }

    public async Task<bool> CanAccessOrganizationalUnitAsync(
        Guid userId,
        Guid organizationalUnitId,
        CancellationToken cancellationToken)
    {
        UserAuthorizationAssignment? assignment = await GetUserAssignmentAsync(userId, cancellationToken);

        return assignment is not null && await AssignmentContainsAsync(
            assignment.ScopeType,
            assignment.ScopeId,
            ScopeType.OrganizationalUnit,
            organizationalUnitId,
            cancellationToken);
    }

    public async Task<bool> CanAccessWarehouseAsync(
        Guid userId,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        UserAuthorizationAssignment? assignment = await GetUserAssignmentAsync(userId, cancellationToken);

        return assignment is not null && await AssignmentContainsAsync(
            assignment.ScopeType,
            assignment.ScopeId,
            ScopeType.Warehouse,
            warehouseId,
            cancellationToken);
    }

    public async Task<bool> CanAccessPartyAsync(
        Guid userId,
        PartyType partyType,
        Guid partyId,
        CancellationToken cancellationToken)
    {
        UserAuthorizationAssignment? assignment = await GetUserAssignmentAsync(userId, cancellationToken);

        if (assignment is null)
        {
            return false;
        }

        if (assignment.ScopeType == ScopeType.Enterprise)
        {
            return true;
        }

        if (partyType == PartyType.External)
        {
            return true;
        }

        if (partyType == PartyType.Site)
        {
            return await CanAccessSiteAsync(userId, partyId, cancellationToken);
        }

        Guid? organizationalUnitId = partyType switch
        {
            PartyType.OrganizationalUnit => partyId,
            PartyType.Employee => await context.Employees
                .AsNoTracking()
                .Where(employee => employee.Id == partyId)
                .Select(employee => (Guid?)employee.OrgUnitId)
                .SingleOrDefaultAsync(cancellationToken),
            _ => null
        };

        if (!organizationalUnitId.HasValue)
        {
            return false;
        }

        if (assignment.ScopeType == ScopeType.Warehouse && assignment.ScopeId.HasValue)
        {
            Guid? owningOrganizationalUnitId = await context.Warehouses
                .AsNoTracking()
                .Where(warehouse => warehouse.Id == assignment.ScopeId.Value)
                .Select(warehouse => warehouse.OrganizationalUnitId)
                .SingleOrDefaultAsync(cancellationToken);

            return owningOrganizationalUnitId.HasValue &&
                   await IsOrganizationalUnitDescendantAsync(
                       owningOrganizationalUnitId.Value,
                       organizationalUnitId.Value,
                       cancellationToken);
        }

        return await AssignmentContainsAsync(
            assignment.ScopeType,
            assignment.ScopeId,
            ScopeType.OrganizationalUnit,
            organizationalUnitId,
            cancellationToken);
    }

    public async Task<PartyAccessScope> GetPartyAccessScopeAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        UserAuthorizationAssignment? assignment = await GetUserAssignmentAsync(userId, cancellationToken);

        if (assignment is null)
        {
            return new PartyAccessScope(false, false, new HashSet<Guid>(), new HashSet<Guid>());
        }

        if (assignment.ScopeType == ScopeType.Enterprise)
        {
            return new PartyAccessScope(true, true, new HashSet<Guid>(), new HashSet<Guid>());
        }

        if (!assignment.ScopeId.HasValue)
        {
            return new PartyAccessScope(true, false, new HashSet<Guid>(), new HashSet<Guid>());
        }

        Guid? siteId;
        Guid[] organizationalUnitIds;

        if (assignment.ScopeType == ScopeType.Site)
        {
            siteId = assignment.ScopeId.Value;
            organizationalUnitIds = await context.OrganizationalUnits
                .AsNoTracking()
                .Where(unit => unit.SiteId == siteId.Value)
                .Select(unit => unit.Id)
                .ToArrayAsync(cancellationToken);
        }
        else if (assignment.ScopeType == ScopeType.OrganizationalUnit)
        {
            siteId = await context.OrganizationalUnits
                .AsNoTracking()
                .Where(unit => unit.Id == assignment.ScopeId.Value)
                .Select(unit => (Guid?)unit.SiteId)
                .SingleOrDefaultAsync(cancellationToken);
            organizationalUnitIds = await GetDescendantOrganizationalUnitIdsAsync(
                assignment.ScopeId.Value,
                cancellationToken);
        }
        else
        {
            var warehouse = await context.Warehouses
                .AsNoTracking()
                .Where(item => item.Id == assignment.ScopeId.Value)
                .Select(item => new { item.SiteId, item.OrganizationalUnitId })
                .SingleOrDefaultAsync(cancellationToken);

            siteId = warehouse?.SiteId;
            organizationalUnitIds = warehouse?.OrganizationalUnitId is Guid organizationalUnitId
                ? await GetDescendantOrganizationalUnitIdsAsync(organizationalUnitId, cancellationToken)
                : [];
        }

        return new PartyAccessScope(
            true,
            false,
            siteId.HasValue ? new HashSet<Guid> { siteId.Value } : new HashSet<Guid>(),
            organizationalUnitIds.ToHashSet());
    }

    public async Task<SitePermissionScope> GetSitePermissionScopeAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        UserPermissionScopeGrant? grant = await GetGrantAsync(userId, permission, cancellationToken);

        if (grant is null)
        {
            return new SitePermissionScope(false, new HashSet<Guid>());
        }

        if (grant.ScopeType == ScopeType.Enterprise)
        {
            return new SitePermissionScope(true, new HashSet<Guid>());
        }

        Guid? siteId = grant.ScopeType switch
        {
            ScopeType.Site => grant.ScopeId,
            ScopeType.OrganizationalUnit => await context.OrganizationalUnits
                .AsNoTracking()
                .Where(unit => unit.Id == grant.ScopeId)
                .Select(unit => (Guid?)unit.SiteId)
                .SingleOrDefaultAsync(cancellationToken),
            ScopeType.Warehouse => await context.Warehouses
                .AsNoTracking()
                .Where(warehouse => warehouse.Id == grant.ScopeId)
                .Select(warehouse => (Guid?)warehouse.SiteId)
                .SingleOrDefaultAsync(cancellationToken),
            _ => null
        };

        return new SitePermissionScope(
            false,
            siteId.HasValue ? new HashSet<Guid> { siteId.Value } : new HashSet<Guid>());
    }

    public async Task<OrganizationalUnitPermissionScope> GetOrganizationalUnitPermissionScopeAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        UserPermissionScopeGrant? grant = await GetGrantAsync(userId, permission, cancellationToken);

        if (grant is null)
        {
            return new OrganizationalUnitPermissionScope(false, new HashSet<Guid>());
        }

        if (grant.ScopeType == ScopeType.Enterprise)
        {
            return new OrganizationalUnitPermissionScope(true, new HashSet<Guid>());
        }

        Guid[] unitIds = grant.ScopeType switch
        {
            ScopeType.Site when grant.ScopeId.HasValue => await context.OrganizationalUnits
                .AsNoTracking()
                .Where(unit => unit.SiteId == grant.ScopeId.Value)
                .Select(unit => unit.Id)
                .ToArrayAsync(cancellationToken),
            ScopeType.OrganizationalUnit when grant.ScopeId.HasValue =>
                await GetDescendantOrganizationalUnitIdsAsync(grant.ScopeId.Value, cancellationToken),
            _ => []
        };

        return new OrganizationalUnitPermissionScope(false, unitIds.ToHashSet());
    }

    public async Task<WarehousePermissionScope> GetWarehousePermissionScopeAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        UserPermissionScopeGrant? grant = await GetGrantAsync(userId, permission, cancellationToken);

        if (grant is null)
        {
            return new WarehousePermissionScope(false, new HashSet<Guid>());
        }

        if (grant.ScopeType == ScopeType.Enterprise)
        {
            return new WarehousePermissionScope(true, new HashSet<Guid>());
        }

        Guid[] warehouseIds = grant.ScopeType switch
        {
            ScopeType.Site when grant.ScopeId.HasValue => await context.Warehouses
                .AsNoTracking()
                .Where(warehouse => warehouse.SiteId == grant.ScopeId.Value)
                .Select(warehouse => warehouse.Id)
                .ToArrayAsync(cancellationToken),
            ScopeType.OrganizationalUnit when grant.ScopeId.HasValue => await GetOrganizationalUnitWarehouseIdsAsync(
                grant.ScopeId.Value,
                cancellationToken),
            ScopeType.Warehouse when grant.ScopeId.HasValue => [grant.ScopeId.Value],
            _ => []
        };

        return new WarehousePermissionScope(false, warehouseIds.ToHashSet());
    }

    private async Task<bool> AssignmentContainsAsync(
        ScopeType assignmentType,
        Guid? assignmentScopeId,
        ScopeType resourceType,
        Guid? resourceId,
        CancellationToken cancellationToken)
    {
        if (assignmentType == ScopeType.Enterprise)
        {
            return true;
        }

        if (resourceType == ScopeType.Enterprise || resourceId is null || assignmentScopeId is null)
        {
            return false;
        }

        if (assignmentType == resourceType)
        {
            return assignmentType == ScopeType.OrganizationalUnit
                ? await IsOrganizationalUnitDescendantAsync(
                    assignmentScopeId.Value,
                    resourceId.Value,
                    cancellationToken)
                : assignmentScopeId == resourceId;
        }

        if (assignmentType == ScopeType.Site && resourceType == ScopeType.OrganizationalUnit)
        {
            return await context.OrganizationalUnits
                .AsNoTracking()
                .AnyAsync(
                    unit => unit.Id == resourceId.Value && unit.SiteId == assignmentScopeId.Value,
                    cancellationToken);
        }

        if (resourceType != ScopeType.Warehouse)
        {
            return false;
        }

        var warehouse = await context.Warehouses
            .AsNoTracking()
            .Where(item => item.Id == resourceId.Value)
            .Select(item => new { item.SiteId, item.OrganizationalUnitId })
            .SingleOrDefaultAsync(cancellationToken);

        if (warehouse is null)
        {
            return false;
        }

        if (assignmentType == ScopeType.Site)
        {
            return warehouse.SiteId == assignmentScopeId.Value;
        }

        return assignmentType == ScopeType.OrganizationalUnit &&
               warehouse.OrganizationalUnitId.HasValue &&
               await IsOrganizationalUnitDescendantAsync(
                   assignmentScopeId.Value,
                   warehouse.OrganizationalUnitId.Value,
                   cancellationToken);
    }

    private async Task<UserPermissionScopeGrant?> GetGrantAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        List<UserPermissionScopeGrant> grants = await GetAllGrantsAsync(userId, cancellationToken);
        return grants.FirstOrDefault(grant => grant.PermissionCode == permission);
    }

    private async Task<Guid[]> GetOrganizationalUnitWarehouseIdsAsync(
        Guid organizationalUnitId,
        CancellationToken cancellationToken)
    {
        Guid[] unitIds = await GetDescendantOrganizationalUnitIdsAsync(
            organizationalUnitId,
            cancellationToken);

        return await context.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.OrganizationalUnitId.HasValue &&
                                unitIds.Contains(warehouse.OrganizationalUnitId.Value))
            .Select(warehouse => warehouse.Id)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<bool> IsOrganizationalUnitDescendantAsync(
        Guid ancestorId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        Guid[] descendantIds = await GetDescendantOrganizationalUnitIdsAsync(ancestorId, cancellationToken);
        return descendantIds.Contains(candidateId);
    }

    private async Task<Guid[]> GetDescendantOrganizationalUnitIdsAsync(
        Guid organizationalUnitId,
        CancellationToken cancellationToken) =>
        await GetOrCreateAsync(
            $"org-units:descendants:{organizationalUnitId}",
            "organizational_unit_descendants",
            async ct => await context.Database.SqlQuery<Guid>($$"""
                    WITH RECURSIVE descendants AS (
                        SELECT id
                        FROM public.organizational_units
                        WHERE id = {{organizationalUnitId}}

                        UNION ALL

                        SELECT child.id
                        FROM public.organizational_units AS child
                        INNER JOIN descendants AS parent ON child.parent_id = parent.id
                    )
                    SELECT id AS "Value"
                    FROM descendants
                    """)
                .ToArrayAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: ["organizational-units"],
            cancellationToken: cancellationToken);

    private async Task<List<UserPermissionScopeGrant>> GetAllGrantsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await GetOrCreateAsync(
            $"auth:user-grants:{userId}",
            "user_grants",
            async ct => await (
                from user in context.Users.AsNoTracking()
                where user.Id == userId && user.Status == Domain.Users.UserStatus.Active
                join assignment in context.UserRoleScopes.AsNoTracking()
                    on user.Id equals assignment.UserId
                join rolePermission in context.RolePermissions.AsNoTracking()
                    on assignment.RoleId equals rolePermission.RoleId
                join permission in context.Permissions.AsNoTracking()
                    on rolePermission.PermissionId equals permission.Id
                select new UserPermissionScopeGrant(
                    permission.Code,
                    assignment.ScopeType,
                    assignment.ScopeId))
                .ToListAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: [$"user:{userId}", "auth-roles"],
            cancellationToken: cancellationToken);

    private async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        string keyType,
        Func<CancellationToken, Task<T>> factory,
        HybridCacheEntryOptions options,
        IEnumerable<string> tags,
        CancellationToken cancellationToken)
    {
        bool factoryInvoked = false;

        T value = await hybridCache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                factoryInvoked = true;
                AuthorizationCacheMetrics.RecordMiss(keyType);
                return await factory(ct);
            },
            options,
            tags,
            cancellationToken);

        if (!factoryInvoked)
        {
            AuthorizationCacheMetrics.RecordHit(keyType);
        }

        return value;
    }

    private sealed record UserPermissionScopeGrant(
        string PermissionCode,
        ScopeType ScopeType,
        Guid? ScopeId);
}
