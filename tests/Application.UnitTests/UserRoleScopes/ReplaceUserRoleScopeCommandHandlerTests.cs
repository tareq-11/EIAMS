using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.UserRoleScopes.GetByUser;
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
        var command = new ReplaceUserRoleScopeCommand(Guid.NewGuid(), WellKnownRoles.AdministratorId, ScopeType.Enterprise, null, 0);
        ReplaceUserRoleScopeCommandHandler handler = CreateHandler(context);

        Result<UserRoleScopeResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserErrors.NotFound(command.UserId).Code);
    }

    [Fact]
    public async Task Handle_Should_CreateAssignment_WhenNoExistingAssignment()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "user@example.com", "Test", "User", "hash"));
        context.Roles.Add(Role.Create(WellKnownRoles.AdministratorId, "Admin", "مدير النظام", "Admin role"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.AdministratorId, ScopeType.Enterprise));
        await context.SaveChangesAsync();

        var command = new ReplaceUserRoleScopeCommand(userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null, 0);
        ReplaceUserRoleScopeCommandHandler handler = CreateHandler(context);

        Result<UserRoleScopeResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RowVersion.ShouldBe(1);
        UserRoleScope? assignment = await context.UserRoleScopes.SingleOrDefaultAsync(item => item.UserId == userId);
        assignment.ShouldNotBeNull();
        assignment.RoleId.ShouldBe(WellKnownRoles.AdministratorId);
        assignment.ScopeType.ShouldBe(ScopeType.Enterprise);
        assignment.ScopeId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_IncrementRowVersion_AndRejectStaleReplacement()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var replacementRoleId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "versioned@example.com", "Versioned", "User", "hash"));
        context.Roles.Add(Role.Create(WellKnownRoles.AdministratorId, "Admin", "مدير النظام", "Admin role"));
        context.Roles.Add(Role.Create(replacementRoleId, "Viewer", "مشاهد", "Viewer role"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.AdministratorId, ScopeType.Enterprise));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(replacementRoleId, ScopeType.Enterprise));
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null));
        var secondAdminId = Guid.NewGuid();
        context.Users.Add(User.Create(secondAdminId, "second-versioned@example.com", "Second", "Admin", "hash"));
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), secondAdminId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null));
        await context.SaveChangesAsync();
        ReplaceUserRoleScopeCommandHandler handler = CreateHandler(context);

        Result<UserRoleScopeResponse> updated = await handler.Handle(
            new ReplaceUserRoleScopeCommand(userId, replacementRoleId, ScopeType.Enterprise, null, 1),
            CancellationToken.None);
        updated.IsSuccess.ShouldBeTrue();
        updated.Value.RowVersion.ShouldBe(2);

        Result<UserRoleScopeResponse> stale = await handler.Handle(
            new ReplaceUserRoleScopeCommand(userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null, 1),
            CancellationToken.None);
        stale.Error.Code.ShouldBe("UserRoleScopes.RowVersionMismatch");
        stale.Error.Details.ShouldNotBeNull();
        (await context.UserRoleScopes.SingleAsync(item => item.UserId == userId)).RoleId.ShouldBe(replacementRoleId);
    }

    [Fact]
    public async Task Handle_Should_RejectOrganizationalUnitReplacement()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orgUnitId = Guid.NewGuid();

        context.Users.Add(User.Create(userId, "user@example.com", "Test", "User", "hash"));
        context.Roles.Add(Role.Create(WellKnownRoles.AdministratorId, "Admin", "مدير النظام", "Admin role"));
        context.Roles.Add(Role.Create(WellKnownRoles.WarehouseManagerId, "Manager", "مدير المستودع", "Manager role"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.AdministratorId, ScopeType.Enterprise));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.WarehouseManagerId, ScopeType.Warehouse));
        context.Sites.Add(Site.Create(siteId, Guid.NewGuid(), "Main Site", "SITE1", null));
        context.OrganizationalUnits.Add(OrganizationalUnit.Create(orgUnitId, siteId, null, "Directorate", "Directorate"));
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null));
        var secondAdministratorId = Guid.NewGuid();
        context.Users.Add(User.Create(
            secondAdministratorId,
            "second-admin@example.com",
            "Second",
            "Administrator",
            "hash"));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            secondAdministratorId,
            WellKnownRoles.AdministratorId,
            ScopeType.Enterprise,
            null));
        await context.SaveChangesAsync();

        var command = new ReplaceUserRoleScopeCommand(userId, WellKnownRoles.WarehouseManagerId, ScopeType.OrganizationalUnit, orgUnitId, 1);
        ReplaceUserRoleScopeCommandHandler handler = CreateHandler(context);

        Result<UserRoleScopeResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserRoleScopeErrors.OrganizationalUnitAssignmentNotAllowed);
        (await context.UserRoleScopes.CountAsync(item => item.UserId == userId)).ShouldBe(1);
        UserRoleScope? updatedAssignment = await context.UserRoleScopes.SingleOrDefaultAsync(item => item.UserId == userId);
        updatedAssignment.ShouldNotBeNull();
        updatedAssignment.RoleId.ShouldBe(WellKnownRoles.AdministratorId);
        updatedAssignment.ScopeType.ShouldBe(ScopeType.Enterprise);
        updatedAssignment.ScopeId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_PreventReplacing_LastActiveEnterpriseAdministrator()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var replacementRoleId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "last-admin@example.com", "Last", "Administrator", "hash"));
        context.Roles.Add(Role.Create(WellKnownRoles.AdministratorId, "Admin", "مدير النظام", "Admin role"));
        context.Roles.Add(Role.Create(replacementRoleId, "Viewer", "مشاهد", "Replacement role"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.AdministratorId, ScopeType.Enterprise));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(replacementRoleId, ScopeType.Enterprise));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            userId,
            WellKnownRoles.AdministratorId,
            ScopeType.Enterprise,
            null));
        await context.SaveChangesAsync();
        ReplaceUserRoleScopeCommandHandler handler = CreateHandler(context);

        Result<UserRoleScopeResponse> result = await handler.Handle(
            new ReplaceUserRoleScopeCommand(userId, replacementRoleId, ScopeType.Enterprise, null, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserRoleScopeErrors.CannotRemoveLastEnterpriseAdministrator);
    }

    [Fact]
    public async Task Handle_Should_Reject_WhenRoleDoesNotAllowScopeType()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "user@example.com", "Test", "User", "hash"));
        context.Roles.Add(Role.Create(WellKnownRoles.WarehouseKeeperId, "Keeper", "أمين المستودع", "Keeper role"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.WarehouseKeeperId, ScopeType.Warehouse));
        await context.SaveChangesAsync();

        var command = new ReplaceUserRoleScopeCommand(userId, WellKnownRoles.WarehouseKeeperId, ScopeType.Enterprise, null, 0);
        ReplaceUserRoleScopeCommandHandler handler = CreateHandler(context);

        Result<UserRoleScopeResponse> result = await handler.Handle(command, CancellationToken.None);

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
        context.Roles.Add(Role.Create(WellKnownRoles.WarehouseKeeperId, "Keeper", "أمين المستودع", "Keeper role"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(WellKnownRoles.WarehouseKeeperId, ScopeType.Warehouse));
        await context.SaveChangesAsync();

        var command = new ReplaceUserRoleScopeCommand(userId, WellKnownRoles.WarehouseKeeperId, ScopeType.Warehouse, missingWarehouseId, 0);
        ReplaceUserRoleScopeCommandHandler handler = CreateHandler(context);

        Result<UserRoleScopeResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserRoleScopeErrors.ScopeTargetNotFound(missingWarehouseId).Code);
    }

    private static IUserContext CreateUserContext()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        return userContext;
    }

    private static ReplaceUserRoleScopeCommandHandler CreateHandler(TestDbContext context)
    {
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<Result<Application.UserRoleScopes.GetByUser.UserRoleScopeResponse>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result<UserRoleScopeResponse>>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        IApplicationLock applicationLock = Substitute.For<IApplicationLock>();
        applicationLock.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return new ReplaceUserRoleScopeCommandHandler(
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
