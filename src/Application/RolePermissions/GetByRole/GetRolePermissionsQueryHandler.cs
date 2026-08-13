using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RolePermissions.GetByRole;

internal sealed class GetRolePermissionsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRolePermissionsQuery, PagedResult<PermissionResponse>>
{
    public async Task<Result<PagedResult<PermissionResponse>>> Handle(
        GetRolePermissionsQuery query,
        CancellationToken cancellationToken)
    {
        if (!await context.Roles.AnyAsync(r => r.Id == query.RoleId, cancellationToken))
        {
            return Result.Failure<PagedResult<PermissionResponse>>(RoleErrors.NotFound(query.RoleId));
        }

        PagedResult<PermissionResponse> permissions = await (
                from rolePermission in context.RolePermissions
                where rolePermission.RoleId == query.RoleId
                join permission in context.Permissions on rolePermission.PermissionId equals permission.Id
                select new PermissionResponse
                {
                    Id = permission.Id,
                    Code = permission.Code,
                    Description = permission.Description
                })
            .OrderBy(p => p.Code)
            .ThenBy(p => p.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return permissions;
    }
}
