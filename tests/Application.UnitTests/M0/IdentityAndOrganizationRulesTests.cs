using Domain.Common;
using Domain.Employees;
using Domain.Organizations;
using Domain.OrganizationalUnits;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;

namespace Application.UnitTests.M0;

public sealed class IdentityAndOrganizationRulesTests
{
    [Fact]
    public void Organization_Create_Should_StartActiveAndRaiseCreatedEvent()
    {
        var id = Guid.NewGuid();

        var organization = Organization.Create(id, "Main Organization", "MAIN");

        organization.Id.ShouldBe(id);
        organization.Status.ShouldBe(Status.Active);
        organization.DomainEvents.ShouldContain(domainEvent => domainEvent is OrganizationCreatedDomainEvent);
    }

    [Fact]
    public void Organization_SetStatus_Should_IgnoreNoOpChange()
    {
        var organization = Organization.Create(Guid.NewGuid(), "Main Organization", "MAIN");
        organization.ClearDomainEvents();

        organization.SetStatus(Status.Active);

        organization.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Organization_SetStatus_Should_ChangeStateAndRaiseEvent()
    {
        var organization = Organization.Create(Guid.NewGuid(), "Main Organization", "MAIN");
        organization.ClearDomainEvents();

        organization.SetStatus(Status.Inactive);

        organization.Status.ShouldBe(Status.Inactive);
        organization.DomainEvents.ShouldContain(domainEvent => domainEvent is OrganizationStatusChangedDomainEvent);
    }

    [Fact]
    public void OrganizationalUnit_Create_Should_PreserveHierarchy()
    {
        var siteId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        var unit = OrganizationalUnit.Create(
            Guid.NewGuid(),
            siteId,
            parentId,
            "Finance",
            "Department");

        unit.SiteId.ShouldBe(siteId);
        unit.ParentId.ShouldBe(parentId);
        unit.Status.ShouldBe(Status.Active);
        unit.DomainEvents.ShouldContain(domainEvent => domainEvent is OrganizationalUnitCreatedDomainEvent);
    }

    [Fact]
    public void Employee_UpdateDetails_Should_PreserveImmutableEmployeeNumber()
    {
        var employee = Employee.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Old Name",
            "EMP-001",
            "Clerk");
        employee.ClearDomainEvents();

        employee.UpdateDetails("New Name", "Supervisor");

        employee.FullName.ShouldBe("New Name");
        employee.JobTitle.ShouldBe("Supervisor");
        employee.EmployeeNumber.ShouldBe("EMP-001");
        employee.DomainEvents.ShouldContain(domainEvent => domainEvent is EmployeeUpdatedDomainEvent);
    }

    [Fact]
    public void Employee_SetStatus_Should_IgnoreNoOpChange()
    {
        var employee = Employee.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Employee",
            "EMP-001",
            null);
        employee.ClearDomainEvents();

        employee.SetStatus(Status.Active);

        employee.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void User_LinkToEmployee_Should_SetEmployeeAndRaiseEvent()
    {
        var employeeId = Guid.NewGuid();
        var user = User.Create(Guid.NewGuid(), "user@example.com", "Test", "User", "hash");
        user.ClearDomainEvents();

        user.LinkToEmployee(employeeId);

        user.EmployeeId.ShouldBe(employeeId);
        user.DomainEvents.ShouldContain(domainEvent => domainEvent is UserLinkedToEmployeeDomainEvent);
    }

    [Fact]
    public void Role_UpdateDetails_Should_RaiseUpdatedEvent()
    {
        var role = Role.Create(Guid.NewGuid(), "Reader", "Can read");
        role.ClearDomainEvents();

        role.UpdateDetails("Manager", "Can manage");

        role.Name.ShouldBe("Manager");
        role.Description.ShouldBe("Can manage");
        role.DomainEvents.ShouldContain(domainEvent => domainEvent is RoleUpdatedDomainEvent);
    }

    [Theory]
    [InlineData(ScopeType.Enterprise, false)]
    [InlineData(ScopeType.Site, true)]
    [InlineData(ScopeType.Warehouse, true)]
    public void UserRoleScope_Create_Should_PreserveRequestedScope(
        ScopeType scopeType,
        bool hasScopeId)
    {
        Guid? scopeId = hasScopeId ? Guid.NewGuid() : null;

        var scope = UserRoleScope.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            scopeType,
            scopeId);

        scope.ScopeType.ShouldBe(scopeType);
        scope.ScopeId.ShouldBe(scopeId);
        scope.DomainEvents.ShouldContain(domainEvent => domainEvent is UserRoleScopeGrantedDomainEvent);
    }

    [Fact]
    public void UserRoleScope_MarkAsRevoked_Should_RaiseRevokedEvent()
    {
        var scope = UserRoleScope.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ScopeType.Enterprise,
            null);
        scope.ClearDomainEvents();

        scope.MarkAsRevoked();

        scope.DomainEvents.ShouldContain(domainEvent => domainEvent is UserRoleScopeRevokedDomainEvent);
    }
}
