using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Roles;
using Domain.Users;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Register;

internal sealed class RegisterUserCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(u => u.Email == command.Email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        // There is no other bootstrap path into the system: every mutating endpoint is gated by an
        // Enterprise-scoped permission, and no UserRoleScope grants exist until one is created. The
        // very first registered user is granted the seeded Administrator role so the system is usable.
        bool isFirstUser = !await context.Users.AnyAsync(cancellationToken);

        var user = User.Create(
            Guid.NewGuid(),
            command.Email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password));

        context.Users.Add(user);

        if (isFirstUser)
        {
            context.UserRoleScopes.Add(UserRoleScope.Create(
                Guid.NewGuid(),
                user.Id,
                WellKnownRoles.AdministratorId,
                ScopeType.Enterprise,
                scopeId: null));
        }

        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
