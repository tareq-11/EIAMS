using Application.Abstractions.Authentication;
using Application.Users;
using Application.Users.Refresh;
using Application.UnitTests.Abstractions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class RefreshTokenCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenRefreshTokenDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new RefreshTokenCommandHandler(
            context,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>());

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
        await SeedRefreshTokenAsync(context, "expired-token", expiresOnUtc: now.AddDays(-1));

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(
            context,
            Substitute.For<ITokenProvider>(),
            dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("expired-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_Should_RotateTokenAndReturnNewTokens_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        DateTime now = DateTime.UtcNow;
        await SeedRefreshTokenAsync(context, "old-token", expiresOnUtc: now.AddDays(1));

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.Create(Arg.Any<User>()).Returns("new-access-token");
        tokenProvider.GenerateRefreshToken().Returns("new-refresh-token");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new RefreshTokenCommandHandler(context, tokenProvider, dateTimeProvider);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("old-token"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("new-access-token");
        result.Value.RefreshToken.ShouldBe("new-refresh-token");

        RefreshToken stored = await context.RefreshTokens.SingleAsync();
        stored.Token.ShouldBe("new-refresh-token");
        stored.ExpiresOnUtc.ShouldBeGreaterThan(now);
    }

    private static async Task SeedRefreshTokenAsync(TestDbContext context, string token, DateTime expiresOnUtc)
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
            token,
            user.Id,
            expiresOnUtc);

        context.RefreshTokens.Add(refreshToken);

        await context.SaveChangesAsync();
    }
}
