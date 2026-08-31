using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes.GetByUser;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UserRoleScopes.GetAssignment;

internal sealed class GetUserRoleScopeQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetUserRoleScopeQuery, UserRoleScopeResponse>
{
    public async Task<Result<UserRoleScopeResponse>> Handle(
        GetUserRoleScopeQuery query,
        CancellationToken cancellationToken)
    {
        var assignment = await (
                from userRoleScope in context.UserRoleScopes.AsNoTracking()
                where userRoleScope.UserId == query.UserId
                join role in context.Roles.AsNoTracking() on userRoleScope.RoleId equals role.Id
                select new
                {
                    Entity = userRoleScope,
                    RoleName = role.Name
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (assignment is null)
        {
            return Result.Failure<UserRoleScopeResponse>(
                UserRoleScopeErrors.AssignmentNotFound(query.UserId));
        }

        if (query.UserId != userContext.UserId)
        {
            bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
                userContext.UserId,
                PermissionCodes.Roles.View,
                assignment.Entity.ScopeType,
                assignment.Entity.ScopeId,
                cancellationToken);

            if (!authorized)
            {
                return Result.Failure<UserRoleScopeResponse>(UserRoleScopeErrors.Forbidden);
            }
        }

        return new UserRoleScopeResponse
        {
            Id = assignment.Entity.Id,
            RoleId = assignment.Entity.RoleId,
            RoleName = assignment.RoleName,
            ScopeType = assignment.Entity.ScopeType.ToString(),
            ScopeId = assignment.Entity.ScopeId
        };
    }
}
