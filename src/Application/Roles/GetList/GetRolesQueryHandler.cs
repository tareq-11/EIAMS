using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Roles;
using SharedKernel;

namespace Application.Roles.GetList;

internal sealed class GetRolesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRolesQuery, PagedResult<RoleResponse>>
{
    public async Task<Result<PagedResult<RoleResponse>>> Handle(
        GetRolesQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<RoleResponse> roles = await context.Roles
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name,
                NameAr = r.NameAr,
                Description = r.Description,
                RowVersion = r.RowVersion,
                PermissionCodes = context.RolePermissions
                    .Where(item => item.RoleId == r.Id)
                    .Join(
                        context.Permissions,
                        item => item.PermissionId,
                        permission => permission.Id,
                        (_, permission) => permission.Code)
                    .OrderBy(code => code)
                    .ToArray(),
                AllowedScopeTypes = context.RoleAllowedScopeTypes
                    .Where(item => item.RoleId == r.Id)
                    .OrderBy(item => item.ScopeType)
                    .Select(item => item.ScopeType.ToString())
                    .ToArray()
            })
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return roles;
    }
}