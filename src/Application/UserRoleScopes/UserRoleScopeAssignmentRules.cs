using Application.Abstractions.Data;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UserRoleScopes;

internal static class UserRoleScopeAssignmentRules
{
    public static async Task<Error> ValidateAsync(
        IApplicationDbContext context,
        Guid roleId,
        ScopeType scopeType,
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        if (!await context.Roles.AsNoTracking().AnyAsync(role => role.Id == roleId, cancellationToken))
        {
            return RoleErrors.NotFound(roleId);
        }

        bool roleAllowsScope = await context.RoleAllowedScopeTypes
            .AsNoTracking()
            .AnyAsync(
                allowed => allowed.RoleId == roleId && allowed.ScopeType == scopeType,
                cancellationToken);

        if (!roleAllowsScope)
        {
            return UserRoleScopeErrors.RoleNotAllowedAtScope(roleId, scopeType);
        }

        if (scopeType == ScopeType.Enterprise)
        {
            return scopeId is null ? Error.None : UserRoleScopeErrors.ScopeIdMustBeNull;
        }

        if (!scopeId.HasValue)
        {
            return UserRoleScopeErrors.ScopeIdRequired;
        }

        (bool Exists, Status Status) target = scopeType switch
        {
            ScopeType.Site => await context.Sites
                .AsNoTracking()
                .Where(site => site.Id == scopeId.Value)
                .Select(site => new ValueTuple<bool, Status>(true, site.Status))
                .SingleOrDefaultAsync(cancellationToken),
            ScopeType.OrganizationalUnit => await context.OrganizationalUnits
                .AsNoTracking()
                .Where(unit => unit.Id == scopeId.Value)
                .Select(unit => new ValueTuple<bool, Status>(true, unit.Status))
                .SingleOrDefaultAsync(cancellationToken),
            ScopeType.Warehouse => await context.Warehouses
                .AsNoTracking()
                .Where(warehouse => warehouse.Id == scopeId.Value)
                .Select(warehouse => new ValueTuple<bool, Status>(true, warehouse.Status))
                .SingleOrDefaultAsync(cancellationToken),
            _ => default
        };

        if (!target.Exists)
        {
            return UserRoleScopeErrors.ScopeTargetNotFound(scopeId.Value);
        }

        return target.Status == Status.Active
            ? Error.None
            : UserRoleScopeErrors.ScopeTargetInactive(scopeId.Value);
    }
}
