using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RolePermissions.GetByRole;

internal sealed class GetRolePermissionsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRolePermissionsQuery, List<PermissionResponse>>
{
    public async Task<Result<List<PermissionResponse>>> Handle(
        GetRolePermissionsQuery query,
        CancellationToken cancellationToken)
    {
        if (!await context.Roles.AnyAsync(r => r.Id == query.RoleId, cancellationToken))
        {
            return Result.Failure<List<PermissionResponse>>(RoleErrors.NotFound(query.RoleId));
        }

        List<PermissionResponse> permissions = await (
                from rolePermission in context.RolePermissions
                where rolePermission.RoleId == query.RoleId
                join permission in context.Permissions on rolePermission.PermissionId equals permission.Id
                select new PermissionResponse
                {
                    Id = permission.Id,
                    Code = permission.Code,
                    Description = permission.Description
                })
            .ToListAsync(cancellationToken);

        return permissions;
    }
}
