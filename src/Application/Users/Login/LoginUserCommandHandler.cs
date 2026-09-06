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
        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Always run the expensive password verification. Returning before PBKDF2 for an unknown
        // email creates a measurable timing oracle that reveals which accounts exist.
        bool verified = passwordHasher.Verify(command.Password, user?.PasswordHash ?? DummyPasswordHash);

        if (user is null || !verified)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.NotFoundByEmail);
        }

        return await transaction.ExecuteAsync(
            async ct =>
            {
                await applicationLock.AcquireAsync(UserSessionLock.ForUser(user.Id), ct);
                return await IssueTokensAsync(
                    user.Id,
                    email,
                    command.Password,
                    user.PasswordHash,
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
        User? user = await context.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == userId,
            cancellationToken);

        if (user is null || !string.Equals(user.Email, normalizedEmail, StringComparison.Ordinal))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.NotFoundByEmail);
        }

        if (user.Status == UserStatus.Suspended)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.Suspended);
        }

        if (!string.Equals(user.PasswordHash, previouslyVerifiedPasswordHash, StringComparison.Ordinal) &&
            !passwordHasher.Verify(password, user.PasswordHash))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.NotFoundByEmail);
        }

        if (passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.UpgradePasswordHash(passwordHasher.Hash(password));
        }

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

        context.RefreshTokens.Add(refreshTokenEntity);

        auditContext.RecordSynthetic(new AuditSyntheticSubject(
            user.Id,
            "User",
            user.Id,
            AuditActions.Authenticate,
            nameof(LoginUserCommand),
            null));

        await context.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessToken, refreshToken);
    }

    private const int RefreshTokenExpirationInDays = 7;
}
