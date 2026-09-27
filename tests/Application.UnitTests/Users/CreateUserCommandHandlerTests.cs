using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.Users.Create;
using Domain.Common;
using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class CreateUserCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_RejectDuplicateNormalizedUsername_WithoutAddingUser()
    {
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(User.Create(Guid.NewGuid(), "existing@example.com", "test-user", "Existing", "User", "hash"));
        await context.SaveChangesAsync();
        var actorId = Guid.NewGuid();
        IScopeAuthorizationService scopes = Substitute.For<IScopeAuthorizationService>();
        scopes.HasPermissionAsync(actorId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        scopes.GetUserAssignmentAsync(actorId, Arg.Any<CancellationToken>())
            .Returns(new UserAuthorizationAssignment(Guid.NewGuid(), actorId, Guid.NewGuid(), ScopeType.Enterprise, null));
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(actorId);
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<Result<CreateUserResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result<CreateUserResponse>>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        IApplicationLock applicationLock = Substitute.For<IApplicationLock>();
        applicationLock.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var handler = new CreateUserCommandHandler(
            context, transaction, applicationLock, userContext, scopes, Substitute.For<IPasswordHasher>());

        Result<CreateUserResponse> result = await handler.Handle(new CreateUserCommand(
            "new@example.com", "  TEST-USER  ", "New", "User", "Password123!",
            WellKnownRoles.AdministratorId, UserAssignmentScopeType.Enterprise, null), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.UsernameNotUnique);
        (await context.Users.CountAsync()).ShouldBe(1);
    }
}
