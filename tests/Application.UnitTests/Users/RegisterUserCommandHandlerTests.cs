using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.Users.Register;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class RegisterUserCommandHandlerTests : BaseHandlerTest
{
    private static RegisterUserCommand Command =>
        new("test@example.com", "Test", "User", "Password123");

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenSystemIsAlreadyInitialized()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(User.Create(
            Guid.NewGuid(),
            Command.Email,
            "Existing",
            "User",
            "hash"));
        await context.SaveChangesAsync();

        RegisterUserCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.RegistrationClosed);
    }

    [Fact]
    public async Task Handle_Should_CreateUserWithHashedPasswordAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Hash(Command.Password).Returns("hashed-password");

        RegisterUserCommandHandler handler = CreateHandler(context, passwordHasher);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == result.Value);
        user.Email.ShouldBe(Command.Email);
        user.PasswordHash.ShouldBe("hashed-password");
        user.DomainEvents.ShouldContain(domainEvent => domainEvent is UserRegisteredDomainEvent);

        UserRoleScope assignment = await context.UserRoleScopes.SingleAsync(
            item => item.UserId == user.Id);
        assignment.RoleId.ShouldBe(WellKnownRoles.AdministratorId);
        assignment.ScopeType.ShouldBe(ScopeType.Enterprise);
        assignment.ScopeId.ShouldBeNull();
    }

    private static RegisterUserCommandHandler CreateHandler(
        TestDbContext context,
        IPasswordHasher passwordHasher)
    {
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<Result<Guid>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result<Guid>>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        IApplicationLock applicationLock = Substitute.For<IApplicationLock>();
        applicationLock.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return new RegisterUserCommandHandler(context, transaction, applicationLock, passwordHasher);
    }
}
