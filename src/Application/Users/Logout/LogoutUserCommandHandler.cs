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
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IAuditOperationContextAccessor auditContext) : ICommandHandler<LogoutUserCommand>
{
    public Task<Result> Handle(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Task.FromResult(Result.Success());
        }

        string tokenHash = tokenProvider.HashRefreshToken(command.RefreshToken);

        return transaction.ExecuteAsync(ct => LogoutAsync(tokenHash, ct), cancellationToken);
    }

    private async Task<Result> LogoutAsync(string tokenHash, CancellationToken cancellationToken)
    {
        await applicationLock.AcquireAsync(UserSessionLock.ForRefreshToken(tokenHash), cancellationToken);

        Guid? userId = await context.RefreshTokens
            .Where(token => token.Token == tokenHash)
            .Select(token => (Guid?)token.UserId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!userId.HasValue)
        {
            return Result.Success();
        }

        await applicationLock.AcquireAsync(UserSessionLock.ForUser(userId.Value), cancellationToken);

        Guid? sessionId = await context.RefreshTokens
            .Where(token => token.Token == tokenHash)
            .Select(token => (Guid?)token.SessionId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!sessionId.HasValue)
        {
            return Result.Success();
        }

        List<RefreshToken> activeTokens = await context.RefreshTokens
            .Where(token =>
                token.UserId == userId.Value &&
                token.SessionId == sessionId.Value &&
                token.RevokedOnUtc == null)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count > 0)
        {
            DateTime nowUtc = dateTimeProvider.UtcNow;
            foreach (RefreshToken token in activeTokens)
            {
                token.Revoke(nowUtc);
            }

            auditContext.RecordSynthetic(new AuditSyntheticSubject(
                userId.Value,
                "User",
                userId.Value,
                "Logout",
                nameof(LogoutUserCommand),
                null));

            await context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
