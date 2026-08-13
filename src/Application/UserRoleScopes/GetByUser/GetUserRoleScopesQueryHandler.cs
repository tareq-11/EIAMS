using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UserRoleScopes.GetByUser;

internal sealed class GetUserRoleScopesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetUserRoleScopesQuery, PagedResult<UserRoleScopeResponse>>
{
    public async Task<Result<PagedResult<UserRoleScopeResponse>>> Handle(
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
                return Result.Failure<PagedResult<UserRoleScopeResponse>>(UserRoleScopeErrors.Forbidden);
            }
        }

        PagedResult<UserRoleScopeResponse> scopes = await (
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
            .OrderBy(s => s.RoleName)
            .ThenBy(s => s.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return scopes;
    }
}
