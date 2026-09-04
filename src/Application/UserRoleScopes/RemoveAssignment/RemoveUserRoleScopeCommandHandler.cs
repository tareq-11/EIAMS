using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UserRoleScopes.RemoveAssignment;

internal sealed class RemoveUserRoleScopeCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService) : ICommandHandler<RemoveUserRoleScopeCommand>
{
    public Task<Result> Handle(
        RemoveUserRoleScopeCommand command,
        CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            async ct =>
            {
                await applicationLock.AcquireAsync(AdministratorAssignmentSafety.LockKey, ct);
                return await RemoveAsync(command, ct);
            },
            cancellationToken);

    private async Task<Result> RemoveAsync(
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

        if (AdministratorAssignmentSafety.IsEnterpriseAdministrator(
                assignment.RoleId,
                assignment.ScopeType) &&
            await AdministratorAssignmentSafety.IsLastActiveEnterpriseAdministratorAsync(
                context,
                assignment.UserId,
                cancellationToken))
        {
            return Result.Failure(UserRoleScopeErrors.CannotRemoveLastEnterpriseAdministrator);
        }

        assignment.MarkAsRevoked();
        context.UserRoleScopes.Remove(assignment);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

}
