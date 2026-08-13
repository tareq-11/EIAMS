using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Employees.Create;
using Application.Organizations.Create;
using Application.OrganizationalUnits.Create;
using Application.Sites.Create;
using Application.UnitTests.Abstractions;
using Application.UserRoleScopes.Grant;
using Application.Users.LinkEmployee;
using Domain.Common;
using Domain.Employees;
using Domain.Organizations;
using Domain.OrganizationalUnits;
using Domain.Roles;
using Domain.Sites;
using Domain.Users;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.M0;

public sealed class IdentityAndOrganizationHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task CreateOrganization_Should_ReturnForbiddenAndNotPersist_WhenCallerLacksEnterpriseGrant()
    {
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = CreateUserContext();
        IScopeAuthorizationService authorization = CreateAuthorization(false);
        var handler = new CreateOrganizationCommandHandler(context, userContext, authorization);

        Result<Guid> result = await handler.Handle(new CreateOrganizationCommand("Organization", "ORG"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(OrganizationErrors.Forbidden);
        (await context.Organizations.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateOrganization_Should_RejectDuplicateCode()
    {
        await using TestDbContext context = CreateDbContext();
        context.Organizations.Add(Organization.Create(Guid.NewGuid(), "Existing", "ORG"));
        await context.SaveChangesAsync();

        var handler = new CreateOrganizationCommandHandler(context, CreateUserContext(), CreateAuthorization(true));

        Result<Guid> result = await handler.Handle(new CreateOrganizationCommand("Other", "ORG"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(OrganizationErrors.CodeNotUnique);
    }

    [Fact]
    public async Task CreateSite_Should_RejectMissingOrganizationBeforePersisting()
    {
        await using TestDbContext context = CreateDbContext();
        var handler = new CreateSiteCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new CreateSiteCommand(Guid.NewGuid(), "Amman", "AMM", null);

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SiteErrors.OrganizationNotFound(command.OrganizationId));
        (await context.Sites.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateOrganizationalUnit_Should_RejectParentFromAnotherSite()
    {
        await using TestDbContext context = CreateDbContext();
        var organizationId = Guid.NewGuid();
        var requestedSiteId = Guid.NewGuid();
        var otherSiteId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        context.Organizations.Add(Organization.Create(organizationId, "Organization", "ORG"));
        context.Sites.Add(Site.Create(requestedSiteId, organizationId, "Requested", "REQ", null));
        context.Sites.Add(Site.Create(otherSiteId, organizationId, "Other", "OTH", null));
        context.OrganizationalUnits.Add(OrganizationalUnit.Create(parentId, otherSiteId, null, "Parent", "Department"));
        await context.SaveChangesAsync();

        var handler = new CreateOrganizationalUnitCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new CreateOrganizationalUnitCommand(requestedSiteId, parentId, "Child", "Department");

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(OrganizationalUnitErrors.ParentInDifferentSite(parentId));
        (await context.OrganizationalUnits.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateEmployee_Should_RejectDuplicateEmployeeNumber()
    {
        await using TestDbContext context = CreateDbContext();
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        context.Organizations.Add(Organization.Create(organizationId, "Organization", "ORG"));
        context.Sites.Add(Site.Create(siteId, organizationId, "Amman", "AMM", null));
        context.OrganizationalUnits.Add(OrganizationalUnit.Create(unitId, siteId, null, "Finance", "Department"));
        context.Employees.Add(Employee.Create(Guid.NewGuid(), unitId, "Existing", "EMP-1", null));
        await context.SaveChangesAsync();

        var handler = new CreateEmployeeCommandHandler(context, CreateUserContext(), CreateAuthorization(true));

        Result<Guid> result = await handler.Handle(
            new CreateEmployeeCommand(unitId, "Other", "EMP-1", null),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EmployeeErrors.EmployeeNumberNotUnique);
    }

    [Fact]
    public async Task LinkUserToEmployee_Should_RejectEmployeeAlreadyLinkedToAnotherUser()
    {
        await using TestDbContext context = CreateDbContext();
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var existingUser = User.Create(Guid.NewGuid(), "existing@example.com", "Existing", "User", "hash");
        existingUser.LinkToEmployee(employeeId);

        context.Organizations.Add(Organization.Create(organizationId, "Organization", "ORG"));
        context.Sites.Add(Site.Create(siteId, organizationId, "Amman", "AMM", null));
        context.OrganizationalUnits.Add(OrganizationalUnit.Create(unitId, siteId, null, "Finance", "Department"));
        context.Employees.Add(Employee.Create(employeeId, unitId, "Employee", "EMP-1", null));
        context.Users.Add(existingUser);
        context.Users.Add(User.Create(targetUserId, "target@example.com", "Target", "User", "hash"));
        await context.SaveChangesAsync();

        var handler = new LinkUserToEmployeeCommandHandler(context, CreateUserContext(), CreateAuthorization(true));

        Result result = await handler.Handle(new LinkUserToEmployeeCommand(targetUserId, employeeId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.EmployeeAlreadyLinked);
        (await context.Users.SingleAsync(user => user.Id == targetUserId)).EmployeeId.ShouldBeNull();
    }

    [Theory]
    [InlineData(ScopeType.Enterprise, true, false)]
    [InlineData(ScopeType.Site, false, false)]
    [InlineData(ScopeType.Warehouse, false, false)]
    [InlineData(ScopeType.Enterprise, false, true)]
    [InlineData(ScopeType.Site, true, true)]
    public void GrantUserRoleScopeValidator_Should_EnforceScopeIdContract(
        ScopeType scopeType,
        bool hasScopeId,
        bool expectedValid)
    {
        Guid? scopeId = hasScopeId ? Guid.NewGuid() : null;
        var command = new GrantUserRoleScopeCommand(Guid.NewGuid(), Guid.NewGuid(), scopeType, scopeId);

        FluentValidation.Results.ValidationResult validation = new GrantUserRoleScopeCommandValidator().Validate(command);

        validation.IsValid.ShouldBe(expectedValid);
    }

    [Fact]
    public async Task GrantUserRoleScope_Should_RejectMissingSiteTarget()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var missingSiteId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "user@example.com", "User", "One", "hash"));
        context.Roles.Add(Role.Create(roleId, "Reader", null));
        await context.SaveChangesAsync();

        var handler = new GrantUserRoleScopeCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new GrantUserRoleScopeCommand(userId, roleId, ScopeType.Site, missingSiteId);

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserRoleScopeErrors.ScopeTargetNotFound(missingSiteId));
        (await context.UserRoleScopes.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task GrantUserRoleScope_Should_CreateOneGrantAndRejectDuplicate()
    {
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, "user@example.com", "User", "One", "hash"));
        context.Roles.Add(Role.Create(roleId, "Reader", null));
        await context.SaveChangesAsync();

        var handler = new GrantUserRoleScopeCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new GrantUserRoleScopeCommand(userId, roleId, ScopeType.Enterprise, null);

        Result<Guid> created = await handler.Handle(command, CancellationToken.None);
        Result<Guid> duplicate = await handler.Handle(command, CancellationToken.None);

        created.IsSuccess.ShouldBeTrue();
        duplicate.IsFailure.ShouldBeTrue();
        duplicate.Error.ShouldBe(UserRoleScopeErrors.AlreadyGranted);
        (await context.UserRoleScopes.CountAsync()).ShouldBe(1);
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
