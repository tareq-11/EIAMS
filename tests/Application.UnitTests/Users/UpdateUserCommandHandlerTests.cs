using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.Users.Update;
using Domain.Common;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class UpdateUserCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_RejectDuplicateNormalizedUsername_WithoutChangingTarget()
    {
        await using TestDbContext context = CreateDbContext();
        var target = User.Create(Guid.NewGuid(), "target@example.com", "target-user", "Target", "User", "hash");
        context.Users.AddRange(target, User.Create(Guid.NewGuid(), "existing@example.com", "existing-user", "Existing", "User", "hash"));
        await context.SaveChangesAsync();
        var actorId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(actorId);
        IScopeAuthorizationService scopes = Substitute.For<IScopeAuthorizationService>();
        scopes.HasPermissionAsync(actorId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        scopes.GetUserAssignmentAsync(actorId, Arg.Any<CancellationToken>())
            .Returns(new UserAuthorizationAssignment(Guid.NewGuid(), actorId, Guid.NewGuid(), ScopeType.Enterprise, null));
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(call.ArgAt<CancellationToken>(1)));
        IApplicationLock applicationLock = Substitute.For<IApplicationLock>();
        applicationLock.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var handler = new UpdateUserCommandHandler(context, transaction, applicationLock, userContext, scopes, Substitute.For<IDateTimeProvider>());

        Result result = await handler.Handle(new UpdateUserCommand(target.Id, "changed@example.com", "  EXISTING-USER  ", "Changed", "Name", UserStatus.Active), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.UsernameNotUnique);
        (await context.Users.SingleAsync(user => user.Id == target.Id)).Username.ShouldBe("target-user");
    }
}
