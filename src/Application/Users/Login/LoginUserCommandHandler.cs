using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.AuditLogs;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IApplicationLock applicationLock,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IAuditOperationContextAccessor auditContext) : ICommandHandler<LoginUserCommand, AccessTokensResponse>
{
    // A syntactically valid PBKDF2-SHA512 hash used only to equalize the work performed for an
    // unknown email and a wrong password. It is not a credential and cannot authenticate a user.
    private const string DummyPasswordHash =
        "0000000000000000000000000000000000000000000000000000000000000000-" +
        "00000000000000000000000000000000";

    public async Task<Result<AccessTokensResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        string email = User.NormalizeEmail(command.Email);
        long phaseStartedAt = LoginMetrics.Start();
        LoginCredentialSnapshot? credentials = await context.Users
            .AsNoTracking()
            .Where(user => user.Email == email)
            .Select(user => new LoginCredentialSnapshot(user.Id, user.PasswordHash))
            .SingleOrDefaultAsync(cancellationToken);
        LoginMetrics.Record("user_lookup", phaseStartedAt);

        // Always run the expensive password verification. Returning before PBKDF2 for an unknown
        // email creates a measurable timing oracle that reveals which accounts exist.
        phaseStartedAt = LoginMetrics.Start();
        bool verified = passwordHasher.Verify(command.Password, credentials?.PasswordHash ?? DummyPasswordHash);
        LoginMetrics.Record("password_verification", phaseStartedAt);

        if (credentials is null || !verified)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.NotFoundByEmail);
        }

        return await transaction.ExecuteAsync(
            async ct =>
            {
                long lockStartedAt = LoginMetrics.Start();
                await applicationLock.AcquireAsync(UserSessionLock.ForUser(credentials.UserId), ct);
                LoginMetrics.Record("session_lock", lockStartedAt);
                return await IssueTokensAsync(
                    credentials.UserId,
                    email,
                    command.Password,
                    credentials.PasswordHash,
                    ct);
            },
            cancellationToken);
    }

    private async Task<Result<AccessTokensResponse>> IssueTokensAsync(
        Guid userId,
        string normalizedEmail,
        string password,
        string previouslyVerifiedPasswordHash,
        CancellationToken cancellationToken)
    {
        long phaseStartedAt = LoginMetrics.Start();
        User? user = await context.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        LoginMetrics.Record("user_recheck", phaseStartedAt);

        if (user is null || !string.Equals(user.Email, normalizedEmail, StringComparison.Ordinal))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.NotFoundByEmail);
        }

        if (user.Status == UserStatus.Suspended)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.Suspended);
        }

        if (!string.Equals(user.PasswordHash, previouslyVerifiedPasswordHash, StringComparison.Ordinal) &&
            !VerifyChangedPassword(password, user.PasswordHash))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.NotFoundByEmail);
        }

        if (passwordHasher.NeedsRehash(user.PasswordHash))
        {
            phaseStartedAt = LoginMetrics.Start();
            user.UpgradePasswordHash(passwordHasher.Hash(password));
            LoginMetrics.Record("password_rehash", phaseStartedAt);
        }

        phaseStartedAt = LoginMetrics.Start();
        string accessToken = tokenProvider.Create(user);
        string refreshToken = tokenProvider.GenerateRefreshToken();
        string tokenHash = tokenProvider.HashRefreshToken(refreshToken);
        DateTime nowUtc = dateTimeProvider.UtcNow;
        user.RecordSuccessfulLogin(nowUtc);

        var refreshTokenEntity = RefreshToken.Create(
            Guid.NewGuid(),
            tokenHash,
            user.Id,
            nowUtc.AddDays(RefreshTokenExpirationInDays),
            nowUtc);
        LoginMetrics.Record("token_issuance", phaseStartedAt);

        context.RefreshTokens.Add(refreshTokenEntity);

        auditContext.RecordSynthetic(new AuditSyntheticSubject(
            user.Id,
            "User",
            user.Id,
            AuditActions.Authenticate,
            nameof(LoginUserCommand),
            null));

        phaseStartedAt = LoginMetrics.Start();
        await context.SaveChangesAsync(cancellationToken);
        LoginMetrics.Record("persistence", phaseStartedAt);

        return new AccessTokensResponse(accessToken, refreshToken);
    }

    private bool VerifyChangedPassword(string password, string passwordHash)
    {
        long phaseStartedAt = LoginMetrics.Start();
        bool verified = passwordHasher.Verify(password, passwordHash);
        LoginMetrics.Record("password_reverification", phaseStartedAt);
        return verified;
    }

    private const int RefreshTokenExpirationInDays = 7;

    private sealed record LoginCredentialSnapshot(Guid UserId, string PasswordHash);
}
