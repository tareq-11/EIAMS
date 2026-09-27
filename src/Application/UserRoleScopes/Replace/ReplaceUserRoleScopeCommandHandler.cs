using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes;
using Application.UserRoleScopes.GetByUser;
using Domain.Common;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UserRoleScopes.Replace;

internal sealed class ReplaceUserRoleScopeCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService) : ICommandHandler<ReplaceUserRoleScopeCommand, UserRoleScopeResponse>
{
    public Task<Result<UserRoleScopeResponse>> Handle(
        ReplaceUserRoleScopeCommand command,
        CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            async ct =>
            {
                await applicationLock.AcquireAsync(AdministratorAssignmentSafety.LockKey, ct);
                return await ReplaceAsync(command, ct);
            },
            cancellationToken);

    private async Task<Result<UserRoleScopeResponse>> ReplaceAsync(
        ReplaceUserRoleScopeCommand command,
        CancellationToken cancellationToken)
    {
        if (!await context.Users.AsNoTracking().AnyAsync(user => user.Id == command.UserId, cancellationToken))
        {
            return Result.Failure<UserRoleScopeResponse>(UserErrors.NotFound(command.UserId));
        }

        Error validationError = await UserRoleScopeAssignmentRules.ValidateAsync(
            context,
            command.RoleId,
            command.ScopeType,
            command.ScopeId,
            cancellationToken);

        if (validationError != Error.None)
        {
            return Result.Failure<UserRoleScopeResponse>(validationError);
        }

        UserRoleScope? existingAssignment = await context.UserRoleScopes
            .SingleOrDefaultAsync(assignment => assignment.UserId == command.UserId, cancellationToken);

        if (existingAssignment is not null)
        {
            if (existingAssignment.RowVersion != command.ExpectedRowVersion)
            {
                return Result.Failure<UserRoleScopeResponse>(
                    UserRoleScopeErrors.RowVersionMismatch(command.UserId, command.ExpectedRowVersion, existingAssignment.RowVersion));
            }
            bool canManageCurrentAssignment = await scopeAuthorizationService.HasPermissionInScopeAsync(
                userContext.UserId,
                PermissionCodes.Roles.Manage,
                existingAssignment.ScopeType,
                existingAssignment.ScopeId,
                cancellationToken);

            if (!canManageCurrentAssignment)
            {
                return Result.Failure<UserRoleScopeResponse>(UserRoleScopeErrors.AssignmentOutsideAdministratorScope);
            }

            bool removesEnterpriseAdministrator =
                AdministratorAssignmentSafety.IsEnterpriseAdministrator(
                    existingAssignment.RoleId,
                    existingAssignment.ScopeType) &&
                !AdministratorAssignmentSafety.IsEnterpriseAdministrator(
                    command.RoleId,
                    command.ScopeType);

            if (removesEnterpriseAdministrator &&
                await AdministratorAssignmentSafety.IsLastActiveEnterpriseAdministratorAsync(
                    context,
                    existingAssignment.UserId,
                    cancellationToken))
            {
                return Result.Failure<UserRoleScopeResponse>(UserRoleScopeErrors.CannotRemoveLastEnterpriseAdministrator);
            }
        }
        else if (command.ExpectedRowVersion != 0)
        {
            return Result.Failure<UserRoleScopeResponse>(
                UserRoleScopeErrors.RowVersionMismatch(command.UserId, command.ExpectedRowVersion, 0));
        }

        bool canManageRequestedAssignment = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Roles.Manage,
            command.ScopeType,
            command.ScopeId,
            cancellationToken);

        if (!canManageRequestedAssignment)
        {
            return Result.Failure<UserRoleScopeResponse>(UserRoleScopeErrors.AssignmentOutsideAdministratorScope);
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

        string roleName = await context.Roles.Where(role => role.Id == existingAssignment.RoleId)
            .Select(role => role.Name).SingleAsync(cancellationToken);
        return new UserRoleScopeResponse
        {
            Id = existingAssignment.Id,
            RoleId = existingAssignment.RoleId,
            RoleName = roleName,
            ScopeType = existingAssignment.ScopeType.ToAssignmentScopeType(),
            ScopeId = existingAssignment.ScopeId,
            RowVersion = existingAssignment.RowVersion
        };
    }
}
