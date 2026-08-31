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
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<UpdateUserCommand>
{
    public async Task<Result> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
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

        if (command.Status == UserStatus.Suspended && command.UserId == userContext.UserId)
        {
            return Result.Failure(UserErrors.SelfSuspensionNotAllowed);
        }

        string email = User.NormalizeEmail(command.Email);
        bool emailInUse = await context.Users.AnyAsync(
            item => item.Id != command.UserId && item.Email == email,
            cancellationToken);

        if (emailInUse)
        {
            return Result.Failure(UserErrors.EmailNotUnique);
        }

        bool becomingSuspended = user.Status != UserStatus.Suspended && command.Status == UserStatus.Suspended;
        user.UpdateProfile(email, command.FirstName, command.LastName);
        user.SetStatus(command.Status);

        if (becomingSuspended)
        {
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
