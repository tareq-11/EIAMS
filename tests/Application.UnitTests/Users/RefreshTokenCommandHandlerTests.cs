using Application.Abstractions.Authentication;
using Application.UnitTests.Abstractions;
using Application.Users;
using Application.Users.Refresh;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class RefreshTokenCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenRefreshTokenDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.HashRefreshToken(Arg.Any<string>()).Returns(callInfo => "hash:" + callInfo.Arg<string>());

        var handler = new RefreshTokenCommandHandler(
            context,
            tokenProvider,
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("missing-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenRefreshTokenIsExpired()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        await SeedRefreshTokenAsync(context, "hash:expired-token", expiresOnUtc: now.AddDays(-1));

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.HashRefreshToken("expired-token").Returns("hash:expired-token");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(
            context,
            tokenProvider,
            dateTimeProvider,
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("expired-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_Should_DetectReplayAndInvalidateAllActiveTokens_WhenRevokedTokenIsPresented()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "user@example.com", "Test", "User", "hash");
        context.Users.Add(user);

        var revokedToken = RefreshToken.Create(Guid.NewGuid(), "hash:revoked-token", userId, now.AddDays(7), now.AddDays(-1));
        revokedToken.Revoke(now.AddMinutes(-10));

        var activeToken = RefreshToken.Create(Guid.NewGuid(), "hash:active-token", userId, now.AddDays(7), now);

        context.RefreshTokens.AddRange(revokedToken, activeToken);
        await context.SaveChangesAsync();

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.HashRefreshToken("revoked-token").Returns("hash:revoked-token");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(
            context,
            tokenProvider,
            dateTimeProvider,
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("revoked-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);

        RefreshToken activeReloaded = await context.RefreshTokens.SingleAsync(rt => rt.Token == "hash:active-token");
        activeReloaded.RevokedOnUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_Should_RotateTokenAndReturnNewTokens_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        await SeedRefreshTokenAsync(context, "hash:old-token", expiresOnUtc: now.AddDays(1));

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.HashRefreshToken("old-token").Returns("hash:old-token");
        tokenProvider.HashRefreshToken("new-refresh-token").Returns("hash:new-refresh-token");
        tokenProvider.Create(Arg.Any<User>()).Returns("new-access-token");
        tokenProvider.GenerateRefreshToken().Returns("new-refresh-token");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(
            context,
            tokenProvider,
            dateTimeProvider,
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("old-token"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("new-access-token");
        result.Value.RefreshToken.ShouldBe("new-refresh-token");

        List<RefreshToken> stored = await context.RefreshTokens.OrderBy(rt => rt.CreatedOnUtc).ToListAsync();
        stored.Count.ShouldBe(2);

        RefreshToken oldToken = stored[0];
        oldToken.Token.ShouldBe("hash:old-token");
        oldToken.RevokedOnUtc.ShouldBe(now);
        oldToken.ReplacedByToken.ShouldBe("hash:new-refresh-token");

        RefreshToken newToken = stored[1];
        newToken.Token.ShouldBe("hash:new-refresh-token");
        newToken.ExpiresOnUtc.ShouldBeGreaterThan(now);
        newToken.RevokedOnUtc.ShouldBeNull();
    }

    private static async Task SeedRefreshTokenAsync(
        TestDbContext context,
        string tokenHash,
        DateTime expiresOnUtc,
        DateTime createdOnUtc = default)
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@example.com",
            "Test",
            "User",
            "hash");

        context.Users.Add(user);

        var refreshToken = RefreshToken.Create(
            Guid.NewGuid(),
            tokenHash,
            user.Id,
            expiresOnUtc,
            createdOnUtc == default ? DateTime.UtcNow.AddMinutes(-5) : createdOnUtc);

        context.RefreshTokens.Add(refreshToken);

        await context.SaveChangesAsync();
    }
}
