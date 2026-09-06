using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.Users;
using Application.Users.Login;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class LoginUserCommandHandlerTests : BaseHandlerTest
{
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
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByEmail);
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
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByEmail);
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
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("access-token");
        result.Value.RefreshToken.ShouldBe("refresh-token");

        RefreshToken refreshToken = await context.RefreshTokens.SingleAsync();
        refreshToken.Token.ShouldBe("hash:refresh-token");
        refreshToken.ExpiresOnUtc.ShouldBeGreaterThan(dateTimeProvider.UtcNow);
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
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync()).PasswordHash.ShouldBe("versioned-hash");
        passwordHasher.Received(1).Hash(Password);
    }

    [Fact]
    public async Task Handle_Should_RejectOldEmail_WhenEmailChangesBeforeSessionLock()
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
                user.UpdateProfile("changed@example.com", user.FirstName, user.LastName);
                await context.SaveChangesAsync(call.ArgAt<CancellationToken>(1));
            });
        var handler = new LoginUserCommandHandler(
            context,
            CreateTransaction(),
            applicationLock,
            passwordHasher,
            Substitute.For<ITokenProvider>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByEmail);
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
            Substitute.For<Application.Abstractions.Audit.IAuditOperationContextAccessor>());

        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByEmail);
        passwordHasher.Received(1).Verify(Password, "hash");
        passwordHasher.Received(1).Verify(Password, "changed-hash");
        context.RefreshTokens.ShouldBeEmpty();
    }

    private static async Task SeedUserAsync(TestDbContext context)
    {
        context.Users.Add(User.Create(
            Guid.NewGuid(),
            Email,
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
}
