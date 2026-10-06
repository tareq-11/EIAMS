using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.Users.SetStatus;
using Domain.Common;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class SetUserStatusCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_ShouldSuspendTheAccount_WithoutTouchingItsProfile()
    {
        await using TestDbContext context = CreateDbContext();
        var target = User.Create(Guid.NewGuid(), "target@example.com", "target-user", "Target", "User", "hash");
        context.Users.Add(target);
        await context.SaveChangesAsync();

        Result result = await CreateHandler(context, Guid.NewGuid()).Handle(
            new SetUserStatusCommand(target.Id, UserStatus.Suspended, target.RowVersion),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        User stored = await context.Users.SingleAsync(user => user.Id == target.Id);
        stored.Status.ShouldBe(UserStatus.Suspended);
        // Suspension is a lifecycle transition, not a profile edit: it must not
        // require resubmitting fields the caller may not be able to read.
        stored.Username.ShouldBe("target-user");
        stored.Email.ShouldBe("target@example.com");
    }

    [Fact]
    public async Task Handle_ShouldRefuseSelfSuspension()
    {
        await using TestDbContext context = CreateDbContext();
        var actorId = Guid.NewGuid();
        var target = User.Create(actorId, "self@example.com", "self-user", "Self", "User", "hash");
        context.Users.Add(target);
        await context.SaveChangesAsync();

        Result result = await CreateHandler(context, actorId).Handle(
            new SetUserStatusCommand(target.Id, UserStatus.Suspended, target.RowVersion),
            CancellationToken.None);

        result.Error.ShouldBe(UserErrors.SelfSuspensionNotAllowed);
        (await context.Users.SingleAsync(user => user.Id == target.Id)).Status.ShouldBe(UserStatus.Active);
    }

    [Fact]
    public async Task Handle_ShouldRejectUnknownUser()
    {
        await using TestDbContext context = CreateDbContext();

        Result result = await CreateHandler(context, Guid.NewGuid()).Handle(
            new SetUserStatusCommand(Guid.NewGuid(), UserStatus.Suspended, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ShouldRejectAStaleExpectedRowVersion_AndLeaveTheStatusAlone()
    {
        await using TestDbContext context = CreateDbContext();
        var target = User.Create(Guid.NewGuid(), "target@example.com", "target-user", "Target", "User", "hash");
        context.Users.Add(target);
        await context.SaveChangesAsync();

        Result result = await CreateHandler(context, Guid.NewGuid()).Handle(
            new SetUserStatusCommand(target.Id, UserStatus.Suspended, target.RowVersion + 99),
            CancellationToken.None);

        // A suspension must not be applied against a version it did not read.
        result.Error.Code.ShouldBe("Users.RowVersionMismatch");
        (await context.Users.SingleAsync(user => user.Id == target.Id)).Status.ShouldBe(UserStatus.Active);
    }

    private static SetUserStatusCommandHandler CreateHandler(TestDbContext context, Guid actorId)
    {
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

        return new SetUserStatusCommandHandler(
            context,
            transaction,
            applicationLock,
            userContext,
            scopes,
            Substitute.For<IDateTimeProvider>());
    }
}