using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.Users;
using Application.Users.GetSession;
using Application.Users.Login;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class LoginUserCommandHandlerTests : BaseHandlerTest
{
    private const string Username = "test";
    private const string Email = "test@example.com";
    private const string Password = "Password123";

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        var handler = new LoginUserCommandHandler(
            context,
            CreateTransaction(),
            CreateLock(),
            passwordHasher,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>(),
            CreateSessionHandler());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Username, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByUsername);
        passwordHasher.Received(1).Verify(Password, Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenPasswordIsInvalid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var handler = new LoginUserCommandHandler(
            context,
            CreateTransaction(),
            CreateLock(),
            passwordHasher,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>(),
            CreateSessionHandler());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Username, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByUsername);
    }

    [Fact]
    public async Task Handle_Should_ReturnTokensAndPersistRefreshToken_WhenCredentialsAreValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.Create(Arg.Any<User>()).Returns("access-token");
        tokenProvider.GenerateRefreshToken().Returns("refresh-token");
        tokenProvider.HashRefreshToken("refresh-token").Returns("hash:refresh-token");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new LoginUserCommandHandler(
            context,
            CreateTransaction(),
            CreateLock(),
            passwordHasher,
            tokenProvider,
            dateTimeProvider,
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>(),
            CreateSessionHandler());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Username, Password),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("access-token");
        result.Value.RefreshToken.ShouldBe("refresh-token");

        RefreshToken refreshToken = await context.RefreshTokens.SingleAsync();
        refreshToken.Token.ShouldBe("hash:refresh-token");
        refreshToken.ExpiresOnUtc.ShouldBeGreaterThan(dateTimeProvider.UtcNow);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("  TEST  ")]
    [InlineData("  ＴＥＳＴ  ")]
    public async Task Handle_Should_AuthenticateByCanonicalUsername(string credential)
    {
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Password, "hash").Returns(true);
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.Create(Arg.Any<User>()).Returns("access-token");
        tokenProvider.GenerateRefreshToken().Returns("refresh-token");
        tokenProvider.HashRefreshToken("refresh-token").Returns("hash:refresh-token");
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        LoginUserCommandHandler handler = new(context, CreateTransaction(), CreateLock(), passwordHasher,
            tokenProvider, clock, Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>(), CreateSessionHandler());

        Result<AccessTokensResponse> result = await handler.Handle(new LoginUserCommand(credential, Password), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        tokenProvider.Received(1).Create(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_Should_NotAuthenticateByEmail()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Password, Arg.Any<string>()).Returns(false);
        LoginUserCommandHandler handler = new(context, CreateTransaction(), CreateLock(), passwordHasher,
            Substitute.For<ITokenProvider>(), Substitute.For<IDateTimeProvider>(),
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>(), CreateSessionHandler());

        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByUsername);
        passwordHasher.Received(1).Verify(Password, Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_Should_NotIssueTokens_WhenSessionProjectionFails()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Password, "hash").Returns(true);
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        Application.Abstractions.Messaging.IQueryHandler<GetUserSessionQuery, UserSessionResponse> sessionHandler = Substitute.For<Application.Abstractions.Messaging.IQueryHandler<GetUserSessionQuery, UserSessionResponse>>();
        sessionHandler.Handle(Arg.Any<GetUserSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UserSessionResponse>(UserErrors.NotFound(Guid.NewGuid())));

        LoginUserCommandHandler handler = new(context, CreateTransaction(), CreateLock(), passwordHasher,
            tokenProvider, Substitute.For<IDateTimeProvider>(),
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>(), sessionHandler);

        Result<AccessTokensResponse> result = await handler.Handle(new LoginUserCommand(Username, Password), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        context.RefreshTokens.ShouldBeEmpty();
        tokenProvider.DidNotReceive().Create(Arg.Any<User>());
        tokenProvider.DidNotReceive().GenerateRefreshToken();
    }

    [Fact]
    public async Task Handle_Should_UpgradeLegacyPasswordHash_AfterSuccessfulVerification()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Password, "hash").Returns(true);
        passwordHasher.NeedsRehash("hash").Returns(true);
        passwordHasher.Hash(Password).Returns("versioned-hash");
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.Create(Arg.Any<User>()).Returns("access-token");
        tokenProvider.GenerateRefreshToken().Returns("refresh-token");
        tokenProvider.HashRefreshToken("refresh-token").Returns("hash:refresh-token");
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        var handler = new LoginUserCommandHandler(
            context,
            CreateTransaction(),
            CreateLock(),
            passwordHasher,
            tokenProvider,
            dateTimeProvider,
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>(),
            CreateSessionHandler());

        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Username, Password),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync()).PasswordHash.ShouldBe("versioned-hash");
        passwordHasher.Received(1).Hash(Password);
    }

    [Fact]
    public async Task Handle_Should_RejectLookup_WhenUserIsDeletedBeforeSessionLock()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        User user = await context.Users.SingleAsync();
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Password, "hash").Returns(true);
        IApplicationLock applicationLock = Substitute.For<IApplicationLock>();
        applicationLock.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                // Simulate the user being deleted between the credential snapshot and the session
                // lock acquisition: detach and remove so the second lookup returns null.
                context.Users.Remove(user);
                await context.SaveChangesAsync(call.ArgAt<CancellationToken>(1));
            });
        var handler = new LoginUserCommandHandler(
            context,
            CreateTransaction(),
            applicationLock,
            passwordHasher,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>(),
            CreateSessionHandler());

        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Username, Password),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByUsername);
        context.RefreshTokens.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReverifyPassword_WhenHashChangesBeforeSessionLock()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        User user = await context.Users.SingleAsync();
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Password, "hash").Returns(true);
        passwordHasher.Verify(Password, "changed-hash").Returns(false);
        IApplicationLock applicationLock = Substitute.For<IApplicationLock>();
        applicationLock.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                user.UpgradePasswordHash("changed-hash");
                return Task.CompletedTask;
            });
        var handler = new LoginUserCommandHandler(
            context,
            CreateTransaction(),
            applicationLock,
            passwordHasher,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>(),
            CreateSessionHandler());

        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Username, Password),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByUsername);
        passwordHasher.Received(1).Verify(Password, "hash");
        passwordHasher.Received(1).Verify(Password, "changed-hash");
        context.RefreshTokens.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_RecordFixedLoginPhasesWithoutUserIdentifiers()
    {
        var measurements = new ConcurrentBag<(string Phase, int TagCount)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == LoginMetrics.MeterName &&
                instrument.Name == LoginMetrics.InstrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            ReadOnlySpan<KeyValuePair<string, object?>> tagSpan = tags;
            string? phase = null;

            foreach (KeyValuePair<string, object?> tag in tagSpan)
            {
                if (tag.Key == "auth.phase")
                {
                    phase = tag.Value as string;
                }
            }

            measurements.Add((phase ?? string.Empty, tagSpan.Length));
        });
        listener.Start();

        await using TestDbContext context = CreateDbContext();
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        var handler = new LoginUserCommandHandler(
            context,
            CreateTransaction(),
            CreateLock(),
            passwordHasher,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>(),
            CreateSessionHandler());

        await handler.Handle(new LoginUserCommand(Username, Password), CancellationToken.None);

        measurements.ShouldContain(item => item.Phase == "user_lookup");
        measurements.ShouldContain(item => item.Phase == "password_verification");
        measurements.ShouldAllBe(item => item.TagCount == 1);
    }

    private static async Task SeedUserAsync(TestDbContext context)
    {
        context.Users.Add(User.Create(
            Guid.NewGuid(),
            Email,
            Username,
            "Test",
            "User",
            "hash"));

        await context.SaveChangesAsync();
    }

    private static IApplicationTransaction CreateTransaction()
    {
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<Result<AccessTokensResponse>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result<AccessTokensResponse>>>>(0)(
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

    private static Application.Abstractions.Messaging.IQueryHandler<GetUserSessionQuery, UserSessionResponse> CreateSessionHandler()
    {
        Application.Abstractions.Messaging.IQueryHandler<GetUserSessionQuery, UserSessionResponse> handler = Substitute.For<Application.Abstractions.Messaging.IQueryHandler<GetUserSessionQuery, UserSessionResponse>>();
        handler.Handle(Arg.Any<GetUserSessionQuery>(), Arg.Any<CancellationToken>()).Returns(new UserSessionResponse(
            new UserSessionUserDto(Guid.NewGuid(), Email, "Test", "User", null, null),
            new UserSessionRoleDto(Guid.NewGuid(), "Administrator", null),
            new UserSessionScopeDto(Domain.Common.UserAssignmentScopeType.Enterprise, null, "Enterprise"), []));
        return handler;
    }
}
