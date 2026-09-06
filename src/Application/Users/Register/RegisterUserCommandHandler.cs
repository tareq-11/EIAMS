using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Register;

internal sealed class RegisterUserCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    IBootstrapAdministratorAuthorizer bootstrapAuthorizer,
    IPasswordHasher passwordHasher)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    private const string BootstrapLockKey = "security:bootstrap-administrator";

    public Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (!bootstrapAuthorizer.IsAuthorized(command.BootstrapToken))
        {
            return Task.FromResult(Result.Failure<Guid>(UserErrors.RegistrationClosed));
        }

        return transaction.ExecuteAsync(
            async ct =>
            {
                await applicationLock.AcquireAsync(BootstrapLockKey, ct);
                return await RegisterAsync(command, ct);
            },
            cancellationToken);
    }

    private async Task<Result<Guid>> RegisterAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        // This endpoint exists only to bootstrap a brand-new installation. Once the first
        // administrator exists, every account must be provisioned through the protected
        // POST /users administration endpoint and assigned exactly one role/scope.
        if (await context.Users.AnyAsync(cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.RegistrationClosed);
        }

        string email = User.NormalizeEmail(command.Email);

        var user = User.Create(
            Guid.NewGuid(),
            email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password));

        context.Users.Add(user);
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            user.Id,
            WellKnownRoles.AdministratorId,
            ScopeType.Enterprise,
            scopeId: null));

        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
