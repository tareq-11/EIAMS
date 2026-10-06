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
    public async Task Handle_ShouldUpdateMetadata_AndLeaveTheUsernameUntouched()
    {
        await using TestDbContext context = CreateDbContext();
        var target = User.Create(Guid.NewGuid(), "target@example.com", "target-user", "Target", "User", "hash");
        context.Users.Add(target);
        await context.SaveChangesAsync();

        Result result = await CreateHandler(context, out _).Handle(
            new UpdateUserCommand(target.Id, "changed@example.com", "Changed", "Name", target.RowVersion),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        User stored = await context.Users.SingleAsync(user => user.Id == target.Id);
        stored.Email.ShouldBe("changed@example.com");
        stored.FirstName.ShouldBe("Changed");
        stored.LastName.ShouldBe("Name");
        // The login is immutable after creation: metadata editing must never rename an
        // account, because no read projection used to disclose the current value and a
        // broad upsert silently renamed it.
        stored.Username.ShouldBe("target-user");
    }

    [Fact]
    public async Task Handle_ShouldNotChangeStatus_BecauseSuspensionIsItsOwnOperation()
    {
        await using TestDbContext context = CreateDbContext();
        var target = User.Create(Guid.NewGuid(), "target@example.com", "target-user", "Target", "User", "hash");
        context.Users.Add(target);
        await context.SaveChangesAsync();

        Result result = await CreateHandler(context, out _).Handle(
            new UpdateUserCommand(target.Id, "target@example.com", "Renamed", "Person", target.RowVersion),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        User stored = await context.Users.SingleAsync(user => user.Id == target.Id);
        stored.Status.ShouldBe(UserStatus.Active);
    }

    [Fact]
    public async Task Handle_ShouldRejectDuplicateEmail_WithoutChangingTarget()
    {
        await using TestDbContext context = CreateDbContext();
        var target = User.Create(Guid.NewGuid(), "target@example.com", "target-user", "Target", "User", "hash");
        context.Users.AddRange(
            target,
            User.Create(Guid.NewGuid(), "existing@example.com", "existing-user", "Existing", "User", "hash"));
        await context.SaveChangesAsync();

        Result result = await CreateHandler(context, out _).Handle(
            new UpdateUserCommand(target.Id, "  EXISTING@EXAMPLE.COM  ", "Changed", "Name", target.RowVersion),
            CancellationToken.None);

        result.Error.ShouldBe(UserErrors.EmailNotUnique);
        (await context.Users.SingleAsync(user => user.Id == target.Id)).Email.ShouldBe("target@example.com");
    }

    [Fact]
    public async Task Handle_ShouldRejectUnknownUser()
    {
        await using TestDbContext context = CreateDbContext();

        Result result = await CreateHandler(context, out _).Handle(
            new UpdateUserCommand(Guid.NewGuid(), "nobody@example.com", "Nobody", "Here", 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldRejectAStaleExpectedRowVersion()
    {
        await using TestDbContext context = CreateDbContext();
        var target = User.Create(Guid.NewGuid(), "target@example.com", "target-user", "Target", "User", "hash");
        context.Users.Add(target);
        await context.SaveChangesAsync();

        Result result = await CreateHandler(context, out _).Handle(
            new UpdateUserCommand(target.Id, "changed@example.com", "Changed", "Name", target.RowVersion + 99),
            CancellationToken.None);

        // A stale save is a conflict, not a silent overwrite.
        result.Error.Code.ShouldBe("Users.RowVersionMismatch");
        User stored = await context.Users.SingleAsync(user => user.Id == target.Id);
        stored.Email.ShouldBe("target@example.com");
        stored.FirstName.ShouldBe("Target");
    }

    private static UpdateUserCommandHandler CreateHandler(TestDbContext context, out Guid actorId)
    {
        actorId = Guid.NewGuid();
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

        return new UpdateUserCommandHandler(context, transaction, applicationLock, userContext, scopes);
    }
}