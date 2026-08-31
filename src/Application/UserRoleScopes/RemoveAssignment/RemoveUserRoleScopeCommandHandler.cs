using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UserRoleScopes.RemoveAssignment;

internal sealed class RemoveUserRoleScopeCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    HybridCache hybridCache) : ICommandHandler<RemoveUserRoleScopeCommand>
{
    public async Task<Result> Handle(
        RemoveUserRoleScopeCommand command,
        CancellationToken cancellationToken)
    {
        UserRoleScope? assignment = await context.UserRoleScopes
            .SingleOrDefaultAsync(item => item.UserId == command.UserId, cancellationToken);

        if (assignment is null)
        {
            return Result.Failure(UserRoleScopeErrors.AssignmentNotFound(command.UserId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Roles.Manage,
            assignment.ScopeType,
            assignment.ScopeId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(UserRoleScopeErrors.AssignmentOutsideAdministratorScope);
        }

        if (assignment.RoleId == WellKnownRoles.AdministratorId &&
            assignment.ScopeType == ScopeType.Enterprise)
        {
            int enterpriseAdministratorCount = await context.UserRoleScopes
                .CountAsync(item => item.RoleId == WellKnownRoles.AdministratorId &&
                                    item.ScopeType == ScopeType.Enterprise,
                    cancellationToken);

            if (enterpriseAdministratorCount == 1)
            {
                return Result.Failure(UserRoleScopeErrors.CannotRemoveLastEnterpriseAdministrator);
            }
        }

        assignment.MarkAsRevoked();
        context.UserRoleScopes.Remove(assignment);

        await context.SaveChangesAsync(cancellationToken);
        await hybridCache.RemoveByTagAsync($"user:{command.UserId}", cancellationToken);
        await hybridCache.RemoveByTagAsync("auth-roles", cancellationToken);

        return Result.Success();
    }
}
