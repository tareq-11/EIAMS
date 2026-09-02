using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UserRoleScopes.Revoke;

internal sealed class RevokeUserRoleScopeCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RevokeUserRoleScopeCommand>
{
    public async Task<Result> Handle(RevokeUserRoleScopeCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Roles.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(UserRoleScopeErrors.Forbidden);
        }

        UserRoleScope? userRoleScope = await context.UserRoleScopes
            .SingleOrDefaultAsync(s => s.Id == command.UserRoleScopeId, cancellationToken);

        if (userRoleScope is null)
        {
            return Result.Failure(UserRoleScopeErrors.NotFound(command.UserRoleScopeId));
        }

        userRoleScope.MarkAsRevoked();

        context.UserRoleScopes.Remove(userRoleScope);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
