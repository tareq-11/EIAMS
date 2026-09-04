using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes;
using Domain.Common;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UserRoleScopes.Revoke;

internal sealed class RevokeUserRoleScopeCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RevokeUserRoleScopeCommand>
{
    public Task<Result> Handle(RevokeUserRoleScopeCommand command, CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            async ct =>
            {
                await applicationLock.AcquireAsync(AdministratorAssignmentSafety.LockKey, ct);
                return await RevokeAsync(command, ct);
            },
            cancellationToken);

    private async Task<Result> RevokeAsync(
        RevokeUserRoleScopeCommand command,
        CancellationToken cancellationToken)
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

        if (AdministratorAssignmentSafety.IsEnterpriseAdministrator(
                userRoleScope.RoleId,
                userRoleScope.ScopeType) &&
            await AdministratorAssignmentSafety.IsLastActiveEnterpriseAdministratorAsync(
                context,
                userRoleScope.UserId,
                cancellationToken))
        {
            return Result.Failure(UserRoleScopeErrors.CannotRemoveLastEnterpriseAdministrator);
        }

        userRoleScope.MarkAsRevoked();

        context.UserRoleScopes.Remove(userRoleScope);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
