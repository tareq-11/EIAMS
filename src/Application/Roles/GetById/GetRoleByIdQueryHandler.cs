using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Roles;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.GetById;

internal sealed class GetRoleByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRoleByIdQuery, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(GetRoleByIdQuery query, CancellationToken cancellationToken)
    {
        RoleResponse? role = await context.Roles
            .Where(r => r.Id == query.RoleId)
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
            .SingleOrDefaultAsync(cancellationToken);

        if (role is null)
        {
            return Result.Failure<RoleResponse>(RoleErrors.NotFound(query.RoleId));
        }

        return role;
    }
}