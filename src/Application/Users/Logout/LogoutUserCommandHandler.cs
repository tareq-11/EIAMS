using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Logout;

internal sealed class LogoutUserCommandHandler(
    IApplicationDbContext context,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IAuditOperationContextAccessor auditContext) : ICommandHandler<LogoutUserCommand>
{
    public async Task<Result> Handle(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result.Success();
        }

        string tokenHash = tokenProvider.HashRefreshToken(command.RefreshToken);

        RefreshToken? refreshToken = await context.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.Token == tokenHash, cancellationToken);

        if (refreshToken is not null && refreshToken.RevokedOnUtc is null)
        {
            DateTime nowUtc = dateTimeProvider.UtcNow;
            refreshToken.Revoke(nowUtc);

            auditContext.RecordSynthetic(new AuditSyntheticSubject(
                refreshToken.UserId,
                "User",
                refreshToken.UserId,
                "Logout",
                nameof(LogoutUserCommand),
                null));

            await context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
