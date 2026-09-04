using SharedKernel;

namespace Domain.Roles;

public static class RoleErrors
{
    public static Error AllowedScopeTypesConflictWithAssignments(Guid roleId) => Error.Conflict(
        "Roles.AllowedScopeTypesConflictWithAssignments",
        $"The allowed scope types for role '{roleId}' would invalidate an existing user assignment");
    public static Error NotFound(Guid roleId) => Error.NotFound(
        "Roles.NotFound",
        $"The role with the Id = '{roleId}' was not found");

    public static readonly Error NameNotUnique = Error.Conflict(
        "Roles.NameNotUnique",
        "The provided role name is not unique");

    public static readonly Error Forbidden = Error.Forbidden(
        "Roles.Forbidden",
        "You are not authorized to manage roles.");

    public static readonly Error BuiltInRoleImmutable = Error.Conflict(
        "Roles.BuiltInRoleImmutable",
        "The built-in Administrator role cannot be modified.");
}
