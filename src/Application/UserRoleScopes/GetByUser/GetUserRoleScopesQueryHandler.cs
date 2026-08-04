using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UserRoleScopes.GetByUser;

internal sealed class GetUserRoleScopesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetUserRoleScopesQuery, List<UserRoleScopeResponse>>
{
    public async Task<Result<List<UserRoleScopeResponse>>> Handle(
        GetUserRoleScopesQuery query,
        CancellationToken cancellationToken)
    {
        List<UserRoleScopeResponse> scopes = await (
                from userRoleScope in context.UserRoleScopes
                where userRoleScope.UserId == query.UserId
                join role in context.Roles on userRoleScope.RoleId equals role.Id
                select new UserRoleScopeResponse
                {
                    Id = userRoleScope.Id,
                    RoleId = role.Id,
                    RoleName = role.Name,
                    ScopeType = userRoleScope.ScopeType.ToString(),
                    ScopeId = userRoleScope.ScopeId
                })
            .ToListAsync(cancellationToken);

        return scopes;
    }
}
