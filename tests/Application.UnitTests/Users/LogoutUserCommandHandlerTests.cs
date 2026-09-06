using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
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
            CreateTransaction(),
            CreateLock(),
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
            CreateTransaction(),
            CreateLock(),
            tokenProvider,
            dateTimeProvider,
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        Result result = await handler.Handle(new LogoutUserCommand("token-to-revoke"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        RefreshToken reloaded = await context.RefreshTokens.SingleAsync();
        reloaded.RevokedOnUtc.ShouldBe(nowUtc);
    }

    [Fact]
    public async Task Handle_Should_KeepOtherSessionActive_WhenLoggingOutCurrentSession()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        DateTime nowUtc = DateTime.UtcNow;
        var user = User.Create(userId, "multi-session@example.com", "Test", "User", "hash");
        var currentSession = RefreshToken.Create(
            Guid.NewGuid(),
            "hash:current-session",
            userId,
            nowUtc.AddDays(7),
            nowUtc);
        var otherSession = RefreshToken.Create(
            Guid.NewGuid(),
            "hash:other-session",
            userId,
            nowUtc.AddDays(7),
            nowUtc);
        context.Users.Add(user);
        context.RefreshTokens.AddRange(currentSession, otherSession);
        await context.SaveChangesAsync();

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.HashRefreshToken("current-session").Returns("hash:current-session");
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);
        var handler = new LogoutUserCommandHandler(
            context,
            CreateTransaction(),
            CreateLock(),
            tokenProvider,
            dateTimeProvider,
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        Result result = await handler.Handle(new LogoutUserCommand("current-session"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        currentSession.RevokedOnUtc.ShouldBe(nowUtc);
        otherSession.RevokedOnUtc.ShouldBeNull();
    }

    private static IApplicationTransaction CreateTransaction()
    {
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        return transaction;
    }

    private static IApplicationLock CreateLock()
    {
        IApplicationLock applicationLock = Substitute.For<IApplicationLock>();
        applicationLock.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return applicationLock;
    }
}
