using Application.Abstractions.Authorization;
using Application.UnitTests.Abstractions;
using Application.Users.GetSession;
using Domain.Common;
using Domain.Employees;
using Domain.OrganizationalUnits;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.UserRoleScopes;
using Domain.Users;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class GetUserSessionQueryHandlerTests : BaseHandlerTest
{
    private static readonly string[] CreatePermissionCodes = ["document.create"];

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        await using TestDbContext context = CreateDbContext();
        var missingUserId = Guid.NewGuid();
        var handler = new GetUserSessionQueryHandler(context, Substitute.For<IEffectivePermissionService>());

        Result<UserSessionResponse> result = await handler.Handle(new GetUserSessionQuery(missingUserId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserErrors.NotFound(missingUserId).Code);
    }

    [Fact]
    public async Task Handle_Should_ReturnNoAssignment_WhenUserHasNoRoleScope()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "user@example.com", "First", "Last", "hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new GetUserSessionQueryHandler(context, Substitute.For<IEffectivePermissionService>());

        Result<UserSessionResponse> result = await handler.Handle(new GetUserSessionQuery(userId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserRoleScopeErrors.NoAssignment(userId).Code);
    }

    [Fact]
    public async Task Handle_Should_ReturnCompleteAuthoritativeSession_WhenUserHasAssignmentAndEmployee()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var orgUnitId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var site = Site.Create(siteId, Guid.NewGuid(), "Central Site", "CEN", null);
        var orgUnit = OrganizationalUnit.Create(orgUnitId, siteId, null, "Finance Directorate", "Directorate");
        var employee = Employee.Create(employeeId, orgUnitId, "Ahmad Ali", "EMP-100", "Finance Officer");
        var user = User.Create(userId, "ahmad@example.com", "Ahmad", "Ali", "hash");
        user.LinkToEmployee(employeeId);

        var role = Role.Create(roleId, "DirectorateManager", "مدير المديرية", "Manager of Directorate");
        var permission = Permission.Create(permissionId, "document.create", "إنشاء السند", "إنشاء مسودات سندات المستودعات وإضافة بنودها.", "Create warehouse documents");
        var rolePermission = RolePermission.Create(roleId, permissionId);
        var assignment = UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Site, siteId);

        context.Sites.Add(site);
        context.OrganizationalUnits.Add(orgUnit);
        context.Employees.Add(employee);
        context.Users.Add(user);
        context.Roles.Add(role);
        context.Permissions.Add(permission);
        context.RolePermissions.Add(rolePermission);
        context.UserRoleScopes.Add(assignment);
        await context.SaveChangesAsync();

        IEffectivePermissionService effectivePermissions = Substitute.For<IEffectivePermissionService>();
        effectivePermissions.GetEffectivePermissionCodesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(CreatePermissionCodes);
        var handler = new GetUserSessionQueryHandler(context, effectivePermissions);

        Result<UserSessionResponse> result = await handler.Handle(new GetUserSessionQuery(userId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.User.Id.ShouldBe(userId);
        result.Value.User.Email.ShouldBe("ahmad@example.com");
        result.Value.User.FirstName.ShouldBe("Ahmad");
        result.Value.User.LastName.ShouldBe("Ali");
        result.Value.User.EmployeeId.ShouldBe(employeeId);
        result.Value.User.EmployeeName.ShouldBe("Ahmad Ali");

        result.Value.Role.Id.ShouldBe(roleId);
        result.Value.Role.Name.ShouldBe("DirectorateManager");
        result.Value.Role.NameAr.ShouldBe("مدير المديرية");

        result.Value.Scope.ScopeType.ShouldBe(UserAssignmentScopeType.Site);
        result.Value.Scope.ScopeId.ShouldBe(siteId);
        result.Value.Scope.ScopeName.ShouldBe("Central Site");

        result.Value.PermissionCodes.ShouldContain("document.create");
    }

    [Fact]
    public async Task Handle_Should_ReturnNoAssignment_WhenOnlyLegacyOrganizationalUnitAssignmentExists()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "legacy-user@example.com", "Legacy", "Scope", "hash"));
        context.Roles.Add(Role.Create(roleId, "Legacy role", "دور قديم", null));
        context.OrganizationalUnits.Add(OrganizationalUnit.Create(
            unitId, Guid.NewGuid(), null, "Legacy unit", "Directorate"));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.OrganizationalUnit, unitId));
        await context.SaveChangesAsync();

        var handler = new GetUserSessionQueryHandler(context, Substitute.For<IEffectivePermissionService>());

        Result<UserSessionResponse> result = await handler.Handle(
            new GetUserSessionQuery(userId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserRoleScopeErrors.NoAssignment(userId).Code);
    }

    [Fact]
    public async Task Handle_Should_ReturnMultipleAssignments_WhenMoreThanOneActiveAssignmentExists()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var firstRoleId = Guid.NewGuid();
        var secondRoleId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "multiple@example.com", "Multiple", "Assignments", "hash"));
        context.Roles.AddRange(
            Role.Create(firstRoleId, "First role", "الدور الأول", null),
            Role.Create(secondRoleId, "Second role", "الدور الثاني", null));
        context.UserRoleScopes.AddRange(
            UserRoleScope.Create(Guid.NewGuid(), userId, firstRoleId, ScopeType.Enterprise, null),
            UserRoleScope.Create(Guid.NewGuid(), userId, secondRoleId, ScopeType.Enterprise, null));
        await context.SaveChangesAsync();

        var handler = new GetUserSessionQueryHandler(context, Substitute.For<IEffectivePermissionService>());
        Result<UserSessionResponse> result = await handler.Handle(
            new GetUserSessionQuery(userId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UserRoleScopeErrors.MultipleAssignments(userId).Code);
    }
}
