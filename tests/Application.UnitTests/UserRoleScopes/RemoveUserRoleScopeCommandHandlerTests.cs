using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.UserRoleScopes.RemoveAssignment;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.UserRoleScopes;

public sealed class RemoveUserRoleScopeCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenAssignmentDoesNotExist()
    {
        await using TestDbContext context = CreateDbContext();
        var targetUserId = Guid.NewGuid();
        RemoveUserRoleScopeCommandHandler handler = CreateHandler(context);

        Result result = await handler.Handle(new RemoveUserRoleScopeCommand(targetUserId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserRoleScopeErrors.AssignmentNotFound(targetUserId).Code);
    }

    [Fact]
    public async Task Handle_Should_PreventRemoving_LastEnterpriseAdministrator()
    {
        await using TestDbContext context = CreateDbContext();
        var adminUserId = Guid.NewGuid();
        var adminUser = User.Create(adminUserId, "admin@example.com", "Admin", "User", "hash");
        var adminAssignment = UserRoleScope.Create(Guid.NewGuid(), adminUserId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null);

        context.Users.Add(adminUser);
        context.UserRoleScopes.Add(adminAssignment);
        await context.SaveChangesAsync();

        RemoveUserRoleScopeCommandHandler handler = CreateHandler(context);

        Result result = await handler.Handle(new RemoveUserRoleScopeCommand(adminUserId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserRoleScopeErrors.CannotRemoveLastEnterpriseAdministrator.Code);
        (await context.UserRoleScopes.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_RemoveAssignment_WhenValid()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = User.Create(userId, "user@example.com", "User", "One", "hash");
        var assignment = UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Enterprise, null);

        context.Users.Add(user);
        context.UserRoleScopes.Add(assignment);
        await context.SaveChangesAsync();

        RemoveUserRoleScopeCommandHandler handler = CreateHandler(context);

        Result result = await handler.Handle(new RemoveUserRoleScopeCommand(userId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await context.UserRoleScopes.CountAsync(item => item.UserId == userId)).ShouldBe(0);
    }

    private static IUserContext CreateUserContext()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        return userContext;
    }

    private static RemoveUserRoleScopeCommandHandler CreateHandler(TestDbContext context)
    {
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        IApplicationLock applicationLock = Substitute.For<IApplicationLock>();
        applicationLock.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return new RemoveUserRoleScopeCommandHandler(
            context,
            transaction,
            applicationLock,
            CreateUserContext(),
            CreateAuthorization(true));
    }

    private static IScopeAuthorizationService CreateAuthorization(bool authorized)
    {
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<ScopeType>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(authorized));
        return authorization;
    }
}
