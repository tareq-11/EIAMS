using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authorization;

internal sealed class PermissionProvider(IApplicationDbContext context)
{
    public async Task<HashSet<string>> GetForUserIdAsync(Guid userId)
    {
        List<string> permissionCodes = await (
                from userRoleScope in context.UserRoleScopes
                where userRoleScope.UserId == userId
                join rolePermission in context.RolePermissions on userRoleScope.RoleId equals rolePermission.RoleId
                join permission in context.Permissions on rolePermission.PermissionId equals permission.Id
                select permission.Code)
            .Distinct()
            .ToListAsync();

        return [.. permissionCodes];
    }
}
