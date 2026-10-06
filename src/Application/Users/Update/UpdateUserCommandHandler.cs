using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Update;

internal sealed class UpdateUserCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService) : ICommandHandler<UpdateUserCommand>
{
    public Task<Result> Handle(UpdateUserCommand command, CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            async ct =>
            {
                await applicationLock.AcquireAsync(UserSessionLock.ForUser(command.UserId), ct);
                return await UpdateAsync(command, ct);
            },
            cancellationToken);

    private async Task<Result> UpdateAsync(
        UpdateUserCommand command,
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

        string email = User.NormalizeEmail(command.Email);
        bool emailInUse = await context.Users.AnyAsync(
            item => item.Id != command.UserId && item.Email == email,
            cancellationToken);

        if (emailInUse)
        {
            return Result.Failure(UserErrors.EmailNotUnique);
        }

        // `username` is intentionally not assigned here: the login is immutable after
        // creation, so this operation cannot silently rename an account.
        user.UpdateProfile(email, user.Username, command.FirstName, command.LastName);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}