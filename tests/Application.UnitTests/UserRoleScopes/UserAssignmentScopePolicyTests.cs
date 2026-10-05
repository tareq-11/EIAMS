using Application.Roles.Create;
using Application.Roles.Update;
using Application.UserRoleScopes.Grant;
using Application.UserRoleScopes.Replace;
using Domain.Common;
using FluentValidation;

namespace Application.UnitTests.UserRoleScopes;

public sealed class UserAssignmentScopePolicyTests
{
    [Fact]
    public void Policy_ShouldAllowOnlyEnterpriseSiteAndWarehouse()
    {
        UserAssignmentScopeTypes.IsAllowed(ScopeType.Enterprise).ShouldBeTrue();
        UserAssignmentScopeTypes.IsAllowed(ScopeType.Site).ShouldBeTrue();
        UserAssignmentScopeTypes.IsAllowed(ScopeType.Warehouse).ShouldBeTrue();
        UserAssignmentScopeTypes.IsAllowed(ScopeType.OrganizationalUnit).ShouldBeFalse();
    }

    [Fact]
    public void AssignmentAndRoleValidators_ShouldRejectOrganizationalUnit()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        bool grantValidation = new GrantUserRoleScopeCommandValidator().Validate(
            new GrantUserRoleScopeCommand(userId, roleId, ScopeType.OrganizationalUnit, unitId))
            .IsValid;
        grantValidation.ShouldBeFalse();
        bool replaceValidation = new ReplaceUserRoleScopeCommandValidator().Validate(
            new ReplaceUserRoleScopeCommand(userId, roleId, ScopeType.OrganizationalUnit, unitId, 1))
            .IsValid;
        replaceValidation.ShouldBeFalse();
        bool createValidation = new CreateRoleCommandValidator().Validate(
            new CreateRoleCommand("Role", "دور", null, [], [ScopeType.OrganizationalUnit]))
            .IsValid;
        createValidation.ShouldBeFalse();
        bool updateValidation = new UpdateRoleCommandValidator().Validate(
            new UpdateRoleCommand(roleId, "Role", "دور", null, 1, [ScopeType.OrganizationalUnit]))
            .IsValid;
        updateValidation.ShouldBeFalse();
    }
}
