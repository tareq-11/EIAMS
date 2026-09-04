using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.AuditLogs;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IAuditOperationContextAccessor auditContext) : ICommandHandler<RefreshTokenCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        string tokenHash = tokenProvider.HashRefreshToken(command.RefreshToken);
        Result<AccessTokensResponse>? refreshResult = null;

        Result transactionResult = await transaction.ExecuteAsync(
            async ct =>
            {
                await applicationLock.AcquireAsync($"security:refresh-token:{tokenHash}", ct);
                refreshResult = await RefreshAsync(tokenHash, ct);

                // Replay detection intentionally changes persistent state while returning an
                // authentication failure. Commit that revocation; only exceptions should roll it back.
                return Result.Success();
            },
            cancellationToken);

        return transactionResult.IsFailure
            ? Result.Failure<AccessTokensResponse>(transactionResult.Error)
            : refreshResult ?? Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
    }

    private async Task<Result<AccessTokensResponse>> RefreshAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        DateTime nowUtc = dateTimeProvider.UtcNow;

        RefreshToken? refreshToken = await context.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.Token == tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        if (refreshToken.User.Status == UserStatus.Suspended)
        {
            if (refreshToken.RevokedOnUtc is null)
            {
                refreshToken.Revoke(nowUtc);
                await context.SaveChangesAsync(cancellationToken);
            }

            return Result.Failure<AccessTokensResponse>(UserErrors.Suspended);
        }

        // Replay detection: If a revoked token is presented again, invalidate all active tokens for this user.
        if (refreshToken.RevokedOnUtc is not null)
        {
            List<RefreshToken> activeTokens = await context.RefreshTokens
                .Where(rt => rt.UserId == refreshToken.UserId && rt.RevokedOnUtc == null)
                .ToListAsync(cancellationToken);

            foreach (RefreshToken token in activeTokens)
            {
                token.Revoke(nowUtc);
            }

            await context.SaveChangesAsync(cancellationToken);

            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        if (refreshToken.ExpiresOnUtc < nowUtc)
        {
            refreshToken.Revoke(nowUtc);
            await context.SaveChangesAsync(cancellationToken);

            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        string accessToken = tokenProvider.Create(refreshToken.User);
        string newRefreshToken = tokenProvider.GenerateRefreshToken();
        string newTokenHash = tokenProvider.HashRefreshToken(newRefreshToken);

        // Rotate the token: mark current token revoked and link to the new token hash.
        refreshToken.RevokeAndRotate(newTokenHash, nowUtc);

        var newRefreshTokenEntity = RefreshToken.Create(
            Guid.NewGuid(),
            newTokenHash,
            refreshToken.UserId,
            nowUtc.AddDays(RefreshTokenExpirationInDays),
            nowUtc);

        context.RefreshTokens.Add(newRefreshTokenEntity);

        auditContext.RecordSynthetic(new AuditSyntheticSubject(
            refreshToken.UserId,
            "User",
            refreshToken.UserId,
            AuditActions.TokenRefresh,
            nameof(RefreshTokenCommand),
            null));

        await context.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessToken, newRefreshToken);
    }

    private const int RefreshTokenExpirationInDays = 7;
}
