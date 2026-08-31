using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UserRoleScopes.Grant;

internal sealed class GrantUserRoleScopeCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    HybridCache hybridCache)
    : ICommandHandler<GrantUserRoleScopeCommand, Guid>
{
    public async Task<Result<Guid>> Handle(GrantUserRoleScopeCommand command, CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(u => u.Id == command.UserId, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.NotFound(command.UserId));
        }

        if (await context.UserRoleScopes.AnyAsync(
                assignment => assignment.UserId == command.UserId,
                cancellationToken))
        {
            return Result.Failure<Guid>(UserRoleScopeErrors.UserAlreadyAssigned);
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

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Roles.Manage,
            command.ScopeType,
            command.ScopeId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(UserRoleScopeErrors.AssignmentOutsideAdministratorScope);
        }

        var userRoleScope = UserRoleScope.Create(
            Guid.NewGuid(),
            command.UserId,
            command.RoleId,
            command.ScopeType,
            command.ScopeId);

        context.UserRoleScopes.Add(userRoleScope);

        await context.SaveChangesAsync(cancellationToken);

        await hybridCache.RemoveByTagAsync($"user:{command.UserId}", cancellationToken);
        await hybridCache.RemoveByTagAsync("auth-roles", cancellationToken);

        return userRoleScope.Id;
    }
}
