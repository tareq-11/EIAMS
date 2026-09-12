using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.RecoverAdministrator;

internal sealed class RecoverAdministratorCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    IAdministratorRecoveryAuthorizer recoveryAuthorizer,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RecoverAdministratorCommand, Guid>
{
    public Task<Result<Guid>> Handle(
        RecoverAdministratorCommand command,
        CancellationToken cancellationToken)
    {
        if (!recoveryAuthorizer.IsAuthorized(command.RecoveryToken))
        {
            return Task.FromResult(Result.Failure<Guid>(UserErrors.AdministratorRecoveryUnavailable));
        }

        return transaction.ExecuteAsync(
            async ct =>
            {
                await applicationLock.AcquireAsync(AdministratorAssignmentSafety.LockKey, ct);
                return await RecoverAsync(command, ct);
            },
            cancellationToken);
    }

    private async Task<Result<Guid>> RecoverAsync(
        RecoverAdministratorCommand command,
        CancellationToken cancellationToken)
    {
        if (await AdministratorAssignmentSafety.HasActiveEnterpriseAdministratorAsync(
                context,
                cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.AdministratorRecoveryUnavailable);
        }

        string email = User.NormalizeEmail(command.Email);
        if (await context.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        var administrator = User.Create(
            Guid.NewGuid(),
            email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password));

        context.Users.Add(administrator);
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            administrator.Id,
            WellKnownRoles.AdministratorId,
            ScopeType.Enterprise,
            scopeId: null));

        DateTime nowUtc = dateTimeProvider.UtcNow;
        List<RefreshToken> activeRefreshTokens = await context.RefreshTokens
            .Where(token => token.RevokedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (RefreshToken refreshToken in activeRefreshTokens)
        {
            refreshToken.Revoke(nowUtc);
        }

        await context.SaveChangesAsync(cancellationToken);
        return administrator.Id;
    }
}
