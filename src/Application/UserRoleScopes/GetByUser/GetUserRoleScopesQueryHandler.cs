using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UserRoleScopes.GetByUser;

internal sealed class GetUserRoleScopesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetUserRoleScopesQuery, List<UserRoleScopeResponse>>
{
    public async Task<Result<List<UserRoleScopeResponse>>> Handle(
        GetUserRoleScopesQuery query,
        CancellationToken cancellationToken)
    {
        if (query.UserId != userContext.UserId)
        {
            bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
                userContext.UserId,
                PermissionCodes.Roles.Manage,
                ScopeType.Enterprise,
                scopeId: null,
                cancellationToken);

            if (!authorized)
            {
                return Result.Failure<List<UserRoleScopeResponse>>(UserRoleScopeErrors.Forbidden);
            }
        }

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
