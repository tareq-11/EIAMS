using Application.Abstractions.Authentication;
using Application.UnitTests.Abstractions;
using Application.Users.Logout;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class LogoutUserCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_Succeed_WhenRefreshTokenIsNull()
    {
        await using TestDbContext context = CreateDbContext();
        var handler = new LogoutUserCommandHandler(
            context,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        Result result = await handler.Handle(new LogoutUserCommand(null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_RevokeActiveToken_WhenValid()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        DateTime nowUtc = DateTime.UtcNow;
        var user = User.Create(userId, "user@example.com", "Test", "User", "hash");
        var token = RefreshToken.Create(Guid.NewGuid(), "hash:token-to-revoke", userId, nowUtc.AddDays(7), nowUtc);

        context.Users.Add(user);
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.HashRefreshToken("token-to-revoke").Returns("hash:token-to-revoke");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);

        var handler = new LogoutUserCommandHandler(
            context,
            tokenProvider,
            dateTimeProvider,
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        Result result = await handler.Handle(new LogoutUserCommand("token-to-revoke"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        RefreshToken reloaded = await context.RefreshTokens.SingleAsync();
        reloaded.RevokedOnUtc.ShouldBe(nowUtc);
    }
}
