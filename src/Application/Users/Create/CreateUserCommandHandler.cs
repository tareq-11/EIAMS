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

namespace Application.Users.Create;

internal sealed class CreateUserCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IPasswordHasher passwordHasher) : ICommandHandler<CreateUserCommand, CreateUserResponse>
{
    public Task<Result<CreateUserResponse>> Handle(CreateUserCommand command, CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            async ct =>
            {
                await applicationLock.AcquireAsync(AdministratorAssignmentSafety.LockKey, ct);
                return await CreateAsync(command, ct);
            }, cancellationToken);

    private async Task<Result<CreateUserResponse>> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        Result authorization = await UserAdministrationAuthorization.EnsureEnterpriseAccessAsync(
            scopeAuthorizationService,
            userContext.UserId,
            cancellationToken);

        if (authorization.IsFailure)
        {
            return Result.Failure<CreateUserResponse>(authorization.Error);
        }

        string email = User.NormalizeEmail(command.Email);
        string username = User.NormalizeUsername(command.Username);
        if (await context.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return Result.Failure<CreateUserResponse>(UserErrors.EmailNotUnique);
        }

        if (await context.Users.AnyAsync(user => user.Username == username, cancellationToken))
        {
            return Result.Failure<CreateUserResponse>(UserErrors.UsernameNotUnique);
        }

        ScopeType scopeType = command.ScopeType.ToPersistedScopeType();
        Error assignmentError = await UserRoleScopeAssignmentRules.ValidateAsync(
            context, command.RoleId, scopeType, command.ScopeId, cancellationToken);
        if (assignmentError != Error.None)
        {
            return Result.Failure<CreateUserResponse>(assignmentError);
        }

        if (!await scopeAuthorizationService.HasPermissionInScopeAsync(
                userContext.UserId,
                PermissionCodes.Roles.Manage,
                scopeType,
                command.ScopeId,
                cancellationToken))
        {
            return Result.Failure<CreateUserResponse>(UserRoleScopeErrors.AssignmentOutsideAdministratorScope);
        }

        var user = User.Create(
            Guid.NewGuid(),
            email,
            username,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password));

        context.Users.Add(user);
        var assignment = UserRoleScope.Create(
            Guid.NewGuid(), user.Id, command.RoleId, scopeType, command.ScopeId);
        context.UserRoleScopes.Add(assignment);
        await context.SaveChangesAsync(cancellationToken);

        string roleName = await context.Roles
            .Where(role => role.Id == command.RoleId)
            .Select(role => role.Name)
            .SingleAsync(cancellationToken);

        return new CreateUserResponse(
            user.Id, user.Email, user.Username, user.FirstName, user.LastName,
            new UserRoleScopeResponse
            {
                Id = assignment.Id,
                RoleId = assignment.RoleId,
                RoleName = roleName,
                ScopeType = assignment.ScopeType.ToAssignmentScopeType(),
                ScopeId = assignment.ScopeId,
                RowVersion = assignment.RowVersion
            });
    }
}
