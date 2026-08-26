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
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IAuditOperationContextAccessor auditContext) : ICommandHandler<LoginUserCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.NotFoundByEmail);
        }

        bool verified = passwordHasher.Verify(command.Password, user.PasswordHash);

        if (!verified)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.NotFoundByEmail);
        }

        string accessToken = tokenProvider.Create(user);
        string refreshToken = tokenProvider.GenerateRefreshToken();

        var refreshTokenEntity = RefreshToken.Create(
            Guid.NewGuid(),
            refreshToken,
            user.Id,
            dateTimeProvider.UtcNow.AddDays(RefreshTokenExpirationInDays));

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
