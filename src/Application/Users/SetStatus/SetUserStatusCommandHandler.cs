using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.SetStatus;

internal sealed class SetUserStatusCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<SetUserStatusCommand>
{
    public Task<Result> Handle(SetUserStatusCommand command, CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            async ct =>
            {
                // The Enterprise-administrator lock is shared with role-scope replacement:
                // suspending the last Enterprise Administrator is the same invariant.
                await applicationLock.AcquireAsync(AdministratorAssignmentSafety.LockKey, ct);
                await applicationLock.AcquireAsync(UserSessionLock.ForUser(command.UserId), ct);
                return await SetStatusAsync(command, ct);
            },
            cancellationToken);

    private async Task<Result> SetStatusAsync(
        SetUserStatusCommand command,
        CancellationToken cancellationToken)
    {
        Result authorization = await UserAdministrationAuthorization.EnsureEnterpriseAccessAsync(
            scopeAuthorizationService,
            userContext.UserId,
            cancellationToken);

        if (authorization.IsFailure)
        {
            return authorization;
        }

        User? user = await context.Users.SingleOrDefaultAsync(
            item => item.Id == command.UserId,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        if (user.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(UserErrors.RowVersionMismatch(
                command.UserId,
                command.ExpectedRowVersion,
                user.RowVersion));
        }

        if (command.Status == UserStatus.Suspended && command.UserId == userContext.UserId)
        {
            return Result.Failure(UserErrors.SelfSuspensionNotAllowed);
        }

        if (command.Status == UserStatus.Suspended &&
            user.Status == UserStatus.Active &&
            await AdministratorAssignmentSafety.IsLastActiveEnterpriseAdministratorAsync(
                context,
                user.Id,
                cancellationToken))
        {
            return Result.Failure(UserRoleScopeErrors.CannotRemoveLastEnterpriseAdministrator);
        }

        bool becomingSuspended = user.Status != UserStatus.Suspended && command.Status == UserStatus.Suspended;
        user.SetStatus(command.Status);

        if (becomingSuspended)
        {
            // Suspension must take effect immediately, so every live refresh token is
            // revoked rather than left to expire on its own.
            List<RefreshToken> activeTokens = await context.RefreshTokens
                .Where(token => token.UserId == user.Id && token.RevokedOnUtc == null)
                .ToListAsync(cancellationToken);

            foreach (RefreshToken token in activeTokens)
            {
                token.Revoke(dateTimeProvider.UtcNow);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}