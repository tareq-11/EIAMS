using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.UnitTests.Abstractions;
using Application.UserRoleScopes.GetAssignment;
using Application.UserRoleScopes.GetByUser;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.UserRoleScopes;

public sealed class GetUserRoleScopeQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenNoAssignmentExists()
    {
        await using TestDbContext context = CreateDbContext();
        var targetUserId = Guid.NewGuid();
        var handler = new GetUserRoleScopeQueryHandler(context, CreateUserContext(targetUserId), CreateAuthorization(true));

        Result<UserRoleScopeResponse> result = await handler.Handle(new GetUserRoleScopeQuery(targetUserId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserRoleScopeErrors.AssignmentNotFound(targetUserId).Code);
    }

    [Fact]
    public async Task Handle_Should_ReturnAssignment_WhenRequestingOwnAssignment()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var role = Role.Create(roleId, "Administrator", "System Admin");
        var user = User.Create(userId, "admin@example.com", "Admin", "User", "hash");
        var assignment = UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Enterprise, null);

        context.Users.Add(user);
        context.Roles.Add(role);
        context.UserRoleScopes.Add(assignment);
        await context.SaveChangesAsync();

        var handler = new GetUserRoleScopeQueryHandler(context, CreateUserContext(userId), CreateAuthorization(false));

        Result<UserRoleScopeResponse> result = await handler.Handle(new GetUserRoleScopeQuery(userId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RoleId.ShouldBe(roleId);
        result.Value.RoleName.ShouldBe("Administrator");
        result.Value.ScopeType.ShouldBe(ScopeType.Enterprise.ToString());
        result.Value.ScopeId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenCallerLacksPermissionToViewOtherUserAssignment()
    {
        await using TestDbContext context = CreateDbContext();
        var callerUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var role = Role.Create(roleId, "Staff", "Staff");
        var user = User.Create(targetUserId, "target@example.com", "Target", "User", "hash");
        var assignment = UserRoleScope.Create(Guid.NewGuid(), targetUserId, roleId, ScopeType.Enterprise, null);

        context.Users.Add(user);
        context.Roles.Add(role);
        context.UserRoleScopes.Add(assignment);
        await context.SaveChangesAsync();

        var handler = new GetUserRoleScopeQueryHandler(context, CreateUserContext(callerUserId), CreateAuthorization(false));

        Result<UserRoleScopeResponse> result = await handler.Handle(new GetUserRoleScopeQuery(targetUserId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserRoleScopeErrors.Forbidden.Code);
    }

    private static IUserContext CreateUserContext(Guid userId)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        return userContext;
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
