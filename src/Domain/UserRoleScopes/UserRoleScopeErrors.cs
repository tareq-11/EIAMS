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

    public static readonly Error ScopeIdRequired = Error.Problem(
        "UserRoleScopes.ScopeIdRequired",
        "A scope id is required for Site and Warehouse scoped grants");

    public static readonly Error ScopeIdMustBeNull = Error.Problem(
        "UserRoleScopes.ScopeIdMustBeNull",
        "A scope id must not be provided for Enterprise scoped grants");

    public static Error ScopeTargetNotFound(Guid scopeId) => Error.NotFound(
        "UserRoleScopes.ScopeTargetNotFound",
        $"The scope target with the Id = '{scopeId}' was not found");

    public static readonly Error Forbidden = Error.Forbidden(
        "UserRoleScopes.Forbidden",
        "You are not authorized to manage role grants.");
}
