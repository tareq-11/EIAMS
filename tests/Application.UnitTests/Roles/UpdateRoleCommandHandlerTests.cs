using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Roles;
using Application.Roles.Update;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.Roles;

public sealed class UpdateRoleCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenRoleDoesNotExist()
    {
        await using TestDbContext context = CreateDbContext();
        var missingRoleId = Guid.NewGuid();
        UpdateRoleCommandHandler handler = CreateHandler(context, authorized: true);

        Result<RoleResponse> result = await handler.Handle(
            new UpdateRoleCommand(missingRoleId, "WH_KEEPER", "أمين المستودع", null, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RoleErrors.NotFound(missingRoleId).Code);
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenActorCannotManageRoles()
    {
        await using TestDbContext context = CreateDbContext();
        Guid roleId = await SeedRoleAsync(context);
        UpdateRoleCommandHandler handler = CreateHandler(context, authorized: false);

        Result<RoleResponse> result = await handler.Handle(
            new UpdateRoleCommand(roleId, "RENAMED", "مُعاد تسميته", null, 1),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RoleErrors.Forbidden.Code);
    }

    [Fact]
    public async Task Handle_Should_RejectStaleRowVersion_AndLeaveTheRoleUnchanged()
    {
        await using TestDbContext context = CreateDbContext();
        Guid roleId = await SeedRoleAsync(context);
        UpdateRoleCommandHandler handler = CreateHandler(context, authorized: true);

        Result<RoleResponse> result = await handler.Handle(
            new UpdateRoleCommand(roleId, "RENAMED_WITHOUT_MATCH", "مُعاد تسميته", "clobbered", 99),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Roles.RowVersionMismatch");

        Role role = await context.Roles.SingleAsync(item => item.Id == roleId);
        role.Name.ShouldBe("WH_KEEPER_TEST");
        role.Description.ShouldBe("Original description");
        role.RowVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnUpdatedProjection_WithIncrementedRowVersion()
    {
        await using TestDbContext context = CreateDbContext();
        Guid roleId = await SeedRoleAsync(context);
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        await context.SaveChangesAsync();
        UpdateRoleCommandHandler handler = CreateHandler(context, authorized: true);

        Result<RoleResponse> result = await handler.Handle(
            new UpdateRoleCommand(
                roleId,
                "RENAMED",
                "مُعاد تسميته",
                "Updated description",
                1,
                [ScopeType.Enterprise, ScopeType.Warehouse]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(roleId);
        result.Value.Name.ShouldBe("RENAMED");
        result.Value.NameAr.ShouldBe("مُعاد تسميته");
        result.Value.Description.ShouldBe("Updated description");
        result.Value.RowVersion.ShouldBe(2);
        result.Value.AllowedScopeTypes.ShouldBe(["Enterprise", "Warehouse"]);
    }

    [Fact]
    public async Task Handle_Should_RejectSecondUpdate_ThatReplaysTheFirstRowVersion()
    {
        await using TestDbContext context = CreateDbContext();
        Guid roleId = await SeedRoleAsync(context);
        UpdateRoleCommandHandler handler = CreateHandler(context, authorized: true);

        Result<RoleResponse> first = await handler.Handle(
            new UpdateRoleCommand(roleId, "FIRST", "الأول", "First write", 1),
            CancellationToken.None);
        first.IsSuccess.ShouldBeTrue();

        // A second client that still holds the version it originally read must lose, rather than
        // silently overwriting the first write.
        Result<RoleResponse> stale = await handler.Handle(
            new UpdateRoleCommand(roleId, "SECOND", "الثاني", "Second write", 1),
            CancellationToken.None);

        stale.IsFailure.ShouldBeTrue();
        stale.Error.Code.ShouldBe("Roles.RowVersionMismatch");

        Role role = await context.Roles.SingleAsync(item => item.Id == roleId);
        role.Name.ShouldBe("FIRST");
        role.RowVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_RejectWideningAllowedScopeTypes_OnASeededRole()
    {
        // The escalation path the owner ruling of 2026-10-05 closes: anyone holding roles.manage
        // could promote a seeded role to a scope it was never meant to occupy.
        await using TestDbContext context = CreateDbContext();
        context.Roles.Add(Role.Create(
            WellKnownRoles.WarehouseKeeperId, "WH_KEEPER", "أمين المستودع", "Seeded keeper role"));
        context.RoleAllowedScopeTypes.Add(
            RoleAllowedScopeType.Create(WellKnownRoles.WarehouseKeeperId, ScopeType.Warehouse));
        await context.SaveChangesAsync();
        UpdateRoleCommandHandler handler = CreateHandler(context, authorized: true);

        Result<RoleResponse> result = await handler.Handle(
            new UpdateRoleCommand(
                WellKnownRoles.WarehouseKeeperId,
                "WH_KEEPER",
                "أمين المستودع",
                "Seeded keeper role",
                1,
                [ScopeType.Warehouse, ScopeType.Enterprise]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RoleErrors.SeededRoleScopeTypesImmutable(
            WellKnownRoles.WarehouseKeeperId, ["Enterprise", "Warehouse"]).Code);
    }

    [Fact]
    public async Task Handle_Should_AcceptResubmittingTheExistingScopeTypes_OnASeededRole()
    {
        // The role form always submits allowedScopeTypes. A client that round-trips the current
        // scopes must therefore still be able to rename a seeded role; only an actual change is
        // refused.
        await using TestDbContext context = CreateDbContext();
        context.Roles.Add(Role.Create(
            WellKnownRoles.AuditorId, "AUDITOR", "مدقق", "Seeded auditor role"));
        context.RoleAllowedScopeTypes.Add(
            RoleAllowedScopeType.Create(WellKnownRoles.AuditorId, ScopeType.Enterprise));
        await context.SaveChangesAsync();
        UpdateRoleCommandHandler handler = CreateHandler(context, authorized: true);

        Result<RoleResponse> result = await handler.Handle(
            new UpdateRoleCommand(
                WellKnownRoles.AuditorId,
                "AUDITOR",
                "مدقق التدقيق",
                "Seeded auditor role",
                1,
                [ScopeType.Enterprise]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.NameAr.ShouldBe("مدقق التدقيق");
    }

    [Fact]
    public async Task Handle_Should_RejectNarrowingAllowedScopeTypes_ThatWouldInvalidateAnAssignment()
    {
        await using TestDbContext context = CreateDbContext();
        Guid roleId = await SeedRoleAsync(context);
        var userId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "keeper@example.com", "Test", "User", "hash"));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, Guid.NewGuid()));
        await context.SaveChangesAsync();
        UpdateRoleCommandHandler handler = CreateHandler(context, authorized: true);

        Result<RoleResponse> result = await handler.Handle(
            new UpdateRoleCommand(roleId, "WH_KEEPER_TEST", "أمين المستودع", null, 1, [ScopeType.Enterprise]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RoleErrors.AllowedScopeTypesConflictWithAssignments(roleId).Code);
    }

    private static async Task<Guid> SeedRoleAsync(TestDbContext context)
    {
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, "WH_KEEPER_TEST", "أمين المستودع", "Original description"));
        await context.SaveChangesAsync();
        return roleId;
    }

    private static UpdateRoleCommandHandler CreateHandler(TestDbContext context, bool authorized)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());

        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<ScopeType>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(authorized));

        return new UpdateRoleCommandHandler(context, userContext, authorization);
    }
}