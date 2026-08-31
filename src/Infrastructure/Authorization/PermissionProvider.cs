using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authorization;

internal sealed class PermissionProvider(IApplicationDbContext context)
{
    public async Task<HashSet<string>> GetForUserIdAsync(Guid userId)
    {
        List<string> permissionCodes = await (
                from user in context.Users
                where user.Id == userId && user.Status == Domain.Users.UserStatus.Active
                join userRoleScope in context.UserRoleScopes on user.Id equals userRoleScope.UserId
                join rolePermission in context.RolePermissions on userRoleScope.RoleId equals rolePermission.RoleId
                join permission in context.Permissions on rolePermission.PermissionId equals permission.Id
                select permission.Code)
            .Distinct()
            .ToListAsync();

        return [.. permissionCodes];
    }
}
