using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.UnitTests.Abstractions;
using Application.UserRoleScopes.Replace;
using Domain.Common;
using Domain.OrganizationalUnits;
using Domain.Roles;
using Domain.Sites;
using Domain.UserRoleScopes;
using Domain.Users;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.UserRoleScopes;

public sealed class ReplaceUserRoleScopeCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        await using TestDbContext context = CreateDbContext();
        var command = new ReplaceUserRoleScopeCommand(Guid.NewGuid(), WellKnownRoles.AdministratorId, ScopeType.Enterprise, null);
        var handler = new ReplaceUserRoleScopeCommandHandler(context, CreateUserContext(), CreateAuthorization(true), CreateCache());

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserErrors.NotFound(command.UserId).Code);
    }

    [Fact]
    public async Task Handle_Should_CreateAssignment_WhenNoExistingAssignment()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "user@example.com", "Test", "User", "hash"));
        context.Roles.Add(Role.Create(WellKnownRoles.AdministratorId, "Admin", "Admin role"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.AdministratorId, ScopeType.Enterprise));
        await context.SaveChangesAsync();

        var command = new ReplaceUserRoleScopeCommand(userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null);
        var handler = new ReplaceUserRoleScopeCommandHandler(context, CreateUserContext(), CreateAuthorization(true), CreateCache());

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        UserRoleScope? assignment = await context.UserRoleScopes.SingleOrDefaultAsync(item => item.UserId == userId);
        assignment.ShouldNotBeNull();
        assignment.RoleId.ShouldBe(WellKnownRoles.AdministratorId);
        assignment.ScopeType.ShouldBe(ScopeType.Enterprise);
        assignment.ScopeId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReplaceExistingAssignment_Atomically()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orgUnitId = Guid.NewGuid();

        context.Users.Add(User.Create(userId, "user@example.com", "Test", "User", "hash"));
        context.Roles.Add(Role.Create(WellKnownRoles.AdministratorId, "Admin", "Admin role"));
        context.Roles.Add(Role.Create(WellKnownRoles.WarehouseManagerId, "Manager", "Manager role"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.AdministratorId, ScopeType.Enterprise));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.WarehouseManagerId, ScopeType.OrganizationalUnit));
        context.Sites.Add(Site.Create(siteId, Guid.NewGuid(), "Main Site", "SITE1", null));
        context.OrganizationalUnits.Add(OrganizationalUnit.Create(orgUnitId, siteId, null, "Directorate", "Directorate"));
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null));
        await context.SaveChangesAsync();

        var command = new ReplaceUserRoleScopeCommand(userId, WellKnownRoles.WarehouseManagerId, ScopeType.OrganizationalUnit, orgUnitId);
        var handler = new ReplaceUserRoleScopeCommandHandler(context, CreateUserContext(), CreateAuthorization(true), CreateCache());

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await context.UserRoleScopes.CountAsync(item => item.UserId == userId)).ShouldBe(1);
        UserRoleScope? updatedAssignment = await context.UserRoleScopes.SingleOrDefaultAsync(item => item.UserId == userId);
        updatedAssignment.ShouldNotBeNull();
        updatedAssignment.RoleId.ShouldBe(WellKnownRoles.WarehouseManagerId);
        updatedAssignment.ScopeType.ShouldBe(ScopeType.OrganizationalUnit);
        updatedAssignment.ScopeId.ShouldBe(orgUnitId);
    }

    [Fact]
    public async Task Handle_Should_Reject_WhenRoleDoesNotAllowScopeType()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "user@example.com", "Test", "User", "hash"));
        context.Roles.Add(Role.Create(WellKnownRoles.WarehouseKeeperId, "Keeper", "Keeper role"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.WarehouseKeeperId, ScopeType.Warehouse));
        await context.SaveChangesAsync();

        var command = new ReplaceUserRoleScopeCommand(userId, WellKnownRoles.WarehouseKeeperId, ScopeType.Enterprise, null);
        var handler = new ReplaceUserRoleScopeCommandHandler(context, CreateUserContext(), CreateAuthorization(true), CreateCache());

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserRoleScopeErrors.RoleNotAllowedAtScope(WellKnownRoles.WarehouseKeeperId, ScopeType.Enterprise).Code);
    }

    [Fact]
    public async Task Handle_Should_Reject_WhenScopeTargetDoesNotExist()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var missingWarehouseId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "user@example.com", "Test", "User", "hash"));
        context.Roles.Add(Role.Create(WellKnownRoles.WarehouseKeeperId, "Keeper", "Keeper role"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.WarehouseKeeperId, ScopeType.Warehouse));
        await context.SaveChangesAsync();

        var command = new ReplaceUserRoleScopeCommand(userId, WellKnownRoles.WarehouseKeeperId, ScopeType.Warehouse, missingWarehouseId);
        var handler = new ReplaceUserRoleScopeCommandHandler(context, CreateUserContext(), CreateAuthorization(true), CreateCache());

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserRoleScopeErrors.ScopeTargetNotFound(missingWarehouseId).Code);
    }

    private static IUserContext CreateUserContext()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
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
