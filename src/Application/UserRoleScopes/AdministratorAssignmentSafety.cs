using Application.Abstractions.Data;
using Domain.Common;
using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.UserRoleScopes;

internal static class AdministratorAssignmentSafety
{
    internal const string LockKey = "security:enterprise-administrator";

    internal static bool IsEnterpriseAdministrator(Guid roleId, ScopeType scopeType) =>
        roleId == WellKnownRoles.AdministratorId && scopeType == ScopeType.Enterprise;

    internal static Task<bool> HasActiveEnterpriseAdministratorAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken) =>
        (from assignment in context.UserRoleScopes.AsNoTracking()
         join user in context.Users.AsNoTracking() on assignment.UserId equals user.Id
         where assignment.RoleId == WellKnownRoles.AdministratorId &&
               assignment.ScopeType == ScopeType.Enterprise &&
               user.Status == UserStatus.Active
         select assignment.Id)
        .AnyAsync(cancellationToken);

    internal static async Task<bool> IsLastActiveEnterpriseAdministratorAsync(
        IApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        bool targetIsActiveAdministrator = await (
                from assignment in context.UserRoleScopes.AsNoTracking()
                join user in context.Users.AsNoTracking() on assignment.UserId equals user.Id
                where assignment.UserId == userId &&
                      assignment.RoleId == WellKnownRoles.AdministratorId &&
                      assignment.ScopeType == ScopeType.Enterprise &&
                      user.Status == UserStatus.Active
                select assignment.Id)
            .AnyAsync(cancellationToken);

        if (!targetIsActiveAdministrator)
        {
            return false;
        }

        int activeAdministratorCount = await (
                from assignment in context.UserRoleScopes.AsNoTracking()
                join user in context.Users.AsNoTracking() on assignment.UserId equals user.Id
                where assignment.RoleId == WellKnownRoles.AdministratorId &&
                      assignment.ScopeType == ScopeType.Enterprise &&
                      user.Status == UserStatus.Active
                select assignment.Id)
            .CountAsync(cancellationToken);

        return activeAdministratorCount <= 1;
    }
}
