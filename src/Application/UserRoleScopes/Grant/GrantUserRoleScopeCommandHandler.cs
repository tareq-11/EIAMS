using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Roles;
using Domain.Users;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UserRoleScopes.Grant;

internal sealed class GrantUserRoleScopeCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<GrantUserRoleScopeCommand, Guid>
{
    public async Task<Result<Guid>> Handle(GrantUserRoleScopeCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Roles.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(UserRoleScopeErrors.Forbidden);
        }

        if (!await context.Users.AnyAsync(u => u.Id == command.UserId, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.NotFound(command.UserId));
        }

        if (!await context.Roles.AnyAsync(r => r.Id == command.RoleId, cancellationToken))
        {
            return Result.Failure<Guid>(RoleErrors.NotFound(command.RoleId));
        }

        // Warehouse-scoped target validation is added once the Warehouse entity exists (M2).
        if (command.ScopeType == ScopeType.Site &&
            !await context.Sites.AnyAsync(s => s.Id == command.ScopeId, cancellationToken))
        {
            return Result.Failure<Guid>(UserRoleScopeErrors.ScopeTargetNotFound(command.ScopeId!.Value));
        }

        bool alreadyGranted = await context.UserRoleScopes.AnyAsync(
            s => s.UserId == command.UserId &&
                 s.RoleId == command.RoleId &&
                 s.ScopeType == command.ScopeType &&
                 s.ScopeId == command.ScopeId,
            cancellationToken);

        if (alreadyGranted)
        {
            return Result.Failure<Guid>(UserRoleScopeErrors.AlreadyGranted);
        }

        var userRoleScope = UserRoleScope.Create(
            Guid.NewGuid(),
            command.UserId,
            command.RoleId,
            command.ScopeType,
            command.ScopeId);

        context.UserRoleScopes.Add(userRoleScope);

        await context.SaveChangesAsync(cancellationToken);

        return userRoleScope.Id;
    }
}
