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
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class GetUserSessionQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        await using TestDbContext context = CreateDbContext();
        var missingUserId = Guid.NewGuid();
        var handler = new GetUserSessionQueryHandler(context);

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

        var handler = new GetUserSessionQueryHandler(context);

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

        var role = Role.Create(roleId, "DirectorateManager", "Manager of Directorate");
        var permission = Permission.Create(permissionId, "warehouse-documents:create", "Create warehouse documents");
        var rolePermission = RolePermission.Create(roleId, permissionId);
        var assignment = UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.OrganizationalUnit, orgUnitId);

        context.Sites.Add(site);
        context.OrganizationalUnits.Add(orgUnit);
        context.Employees.Add(employee);
        context.Users.Add(user);
        context.Roles.Add(role);
        context.Permissions.Add(permission);
        context.RolePermissions.Add(rolePermission);
        context.UserRoleScopes.Add(assignment);
        await context.SaveChangesAsync();

        var handler = new GetUserSessionQueryHandler(context);

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

        result.Value.Scope.ScopeType.ShouldBe(ScopeType.OrganizationalUnit.ToString());
        result.Value.Scope.ScopeId.ShouldBe(orgUnitId);
        result.Value.Scope.ScopeName.ShouldBe("Finance Directorate");

        result.Value.PermissionCodes.ShouldContain("warehouse-documents:create");
    }
}
