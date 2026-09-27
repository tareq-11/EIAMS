using Domain.Common;
using SharedKernel;

namespace Domain.UserRoleScopes;

public static class UserRoleScopeErrors
{
    public static Error NotFound(Guid userRoleScopeId) => Error.NotFound(
        "UserRoleScopes.NotFound",
        $"The user role scope with the Id = '{userRoleScopeId}' was not found");

    public static readonly Error AlreadyGranted = Error.Conflict(
        "UserRoleScopes.AlreadyGranted",
        "The user already has this role granted in this scope");

    public static Error AssignmentNotFound(Guid userId) => Error.NotFound(
        "UserRoleScopes.AssignmentNotFound",
        $"The user with the Id = '{userId}' does not have a role and scope assignment");

    public static Error NoAssignment(Guid userId) => Error.NotFound(
        "UserRoleScopes.NoAssignment",
        $"The user with the Id = '{userId}' has no active role or scope assigned");

    public static Error MultipleAssignments(Guid userId) => Error.Conflict(
        "UserRoleScopes.MultipleAssignments",
        "The user has multiple active role and scope assignments");

    public static readonly Error UserAlreadyAssigned = Error.Conflict(
        "UserRoleScopes.UserAlreadyAssigned",
        "The user already has a role and scope assignment. Replace the assignment instead of granting another one");

    public static readonly Error ScopeIdRequired = Error.Problem(
        "UserRoleScopes.ScopeIdRequired",
        "A scope id is required for Site and Warehouse assignments");

    public static readonly Error OrganizationalUnitAssignmentNotAllowed = Error.Problem(
        "UserRoleScopes.OrganizationalUnitAssignmentNotAllowed",
        "OrganizationalUnit is a business resource and cannot be assigned as a user scope");

    public static readonly Error ScopeIdMustBeNull = Error.Problem(
        "UserRoleScopes.ScopeIdMustBeNull",
        "A scope id must not be provided for Enterprise scoped grants");

    public static Error ScopeTargetNotFound(Guid scopeId) => Error.NotFound(
        "UserRoleScopes.ScopeTargetNotFound",
        $"The scope target with the Id = '{scopeId}' was not found");

    public static Error ScopeTargetInactive(Guid scopeId) => Error.Conflict(
        "UserRoleScopes.ScopeTargetInactive",
        $"The scope target with the Id = '{scopeId}' is inactive");

    public static Error RoleNotAllowedAtScope(Guid roleId, ScopeType scopeType) => Error.Conflict(
        "UserRoleScopes.RoleNotAllowedAtScope",
        $"The role with the Id = '{roleId}' cannot be assigned at the '{scopeType}' scope");

    public static readonly Error AssignmentOutsideAdministratorScope = Error.Forbidden(
        "UserRoleScopes.AssignmentOutsideAdministratorScope",
        "The requested assignment is outside your authorization scope");

    public static readonly Error CannotRemoveLastEnterpriseAdministrator = Error.Conflict(
        "UserRoleScopes.CannotRemoveLastEnterpriseAdministrator",
        "The last Enterprise Administrator assignment cannot be removed");

    public static Error RowVersionMismatch(Guid userId, int expectedRowVersion, int currentRowVersion) => Error.Conflict(
        "UserRoleScopes.RowVersionMismatch",
        "The role and scope assignment was modified by another request",
        new { user_id = userId, expected_row_version = expectedRowVersion, current_row_version = currentRowVersion });

    public static readonly Error ResourceOutsideScope = Error.Forbidden(
        "UserRoleScopes.ResourceOutsideScope",
        "The requested resource is outside your authorization scope");

    public static readonly Error Forbidden = Error.Forbidden(
        "UserRoleScopes.Forbidden",
        "You are not authorized to manage role grants.");
}
