using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UserRoleScopes.Replace;

internal sealed class ReplaceUserRoleScopeCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    HybridCache hybridCache) : ICommandHandler<ReplaceUserRoleScopeCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        ReplaceUserRoleScopeCommand command,
        CancellationToken cancellationToken)
    {
        if (!await context.Users.AsNoTracking().AnyAsync(user => user.Id == command.UserId, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.NotFound(command.UserId));
        }

        Error validationError = await UserRoleScopeAssignmentRules.ValidateAsync(
            context,
            command.RoleId,
            command.ScopeType,
            command.ScopeId,
            cancellationToken);

        if (validationError != Error.None)
        {
            return Result.Failure<Guid>(validationError);
        }

        UserRoleScope? existingAssignment = await context.UserRoleScopes
            .SingleOrDefaultAsync(assignment => assignment.UserId == command.UserId, cancellationToken);

        if (existingAssignment is not null)
        {
            bool canManageCurrentAssignment = await scopeAuthorizationService.HasPermissionInScopeAsync(
                userContext.UserId,
                PermissionCodes.Roles.Manage,
                existingAssignment.ScopeType,
                existingAssignment.ScopeId,
                cancellationToken);

            if (!canManageCurrentAssignment)
            {
                return Result.Failure<Guid>(UserRoleScopeErrors.AssignmentOutsideAdministratorScope);
            }
        }

        bool canManageRequestedAssignment = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Roles.Manage,
            command.ScopeType,
            command.ScopeId,
            cancellationToken);

        if (!canManageRequestedAssignment)
        {
            return Result.Failure<Guid>(UserRoleScopeErrors.AssignmentOutsideAdministratorScope);
        }

        if (existingAssignment is null)
        {
            existingAssignment = UserRoleScope.Create(
                Guid.NewGuid(),
                command.UserId,
                command.RoleId,
                command.ScopeType,
                command.ScopeId);

            context.UserRoleScopes.Add(existingAssignment);
        }
        else
        {
            existingAssignment.ReplaceAssignment(command.RoleId, command.ScopeType, command.ScopeId);
        }

        await context.SaveChangesAsync(cancellationToken);
        await hybridCache.RemoveByTagAsync($"user:{command.UserId}", cancellationToken);
        await hybridCache.RemoveByTagAsync("auth-roles", cancellationToken);

        return existingAssignment.Id;
    }
}
