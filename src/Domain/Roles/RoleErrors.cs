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

    public static Error RowVersionMismatch(Guid roleId, int expected, int? current) => Error.Conflict(
        "Roles.RowVersionMismatch",
        "The role was modified by another request.",
        new { role_id = roleId, expected_row_version = expected, current_row_version = current });

    /// <summary>
    /// One or more submitted codes are not part of the active permission vocabulary. Reported
    /// without applying anything, because a wholesale replacement is all-or-nothing.
    /// </summary>
    public static Error UnknownPermissionCodes(IReadOnlyCollection<string> codes) => Error.Problem(
        "Roles.UnknownPermissionCodes",
        "One or more permission codes are not part of the active permission catalog.",
        new { permission_codes = codes });

    /// <summary>
    /// A submitted code is catalogued but can never take effect for this role, because none of its
    /// allowed scope types overlap the role's. Accepting it would record a grant that grants
    /// nothing at any scope the role can occupy, which is indistinguishable from a working grant in
    /// the audit trail and in every permission surface.
    /// </summary>
    public static Error PermissionCodesNotAllowedForRoleScopes(
        IReadOnlyCollection<IncompatiblePermissionCode> incompatible) => Error.Problem(
        "Roles.PermissionCodesNotAllowedForRoleScopes",
        "One or more permissions cannot be granted to this role because their allowed scope types do not overlap the role's.",
        new { incompatible });

    public static readonly Error NameNotUnique = Error.Conflict(
        "Roles.NameNotUnique",
        "The provided role name is not unique");

    public static readonly Error Forbidden = Error.Forbidden(
        "Roles.Forbidden",
        "You are not authorized to manage roles.");

    public static readonly Error OrganizationalUnitAssignmentNotAllowed = Error.Problem(
        "Roles.OrganizationalUnitAssignmentNotAllowed",
        "OrganizationalUnit is a business resource and cannot be declared as a role assignment scope.");

    public static readonly Error BuiltInRoleImmutable = Error.Conflict(
        "Roles.BuiltInRoleImmutable",
        "The built-in Administrator role cannot be modified.");

    /// <summary>
    /// A seeded reference role was asked to change the scope types it may be assigned at.
    /// <para>
    /// Reported as a conflict rather than a validation error because the request is well-formed and
    /// would be accepted for any other role; what is refused is the privilege it would grant, not its
    /// shape. <paramref name="roleId"/> and the submitted set are returned so a client can show what
    /// was attempted instead of only that something was.
    /// </para>
    /// </summary>
    public static Error SeededRoleScopeTypesImmutable(
        Guid roleId,
        IReadOnlyCollection<string> requestedScopeTypes) => Error.Conflict(
        "Roles.SeededRoleScopeTypesImmutable",
        "The scope types of a seeded reference role are fixed at creation and cannot be changed.",
        new { role_id = roleId, requested_scope_types = requestedScopeTypes });

    /// <summary>
    /// A permission code paired with the scope types it is valid at, so a rejected request can say
    /// which code conflicted and why instead of only that something did.
    /// </summary>
    public sealed record IncompatiblePermissionCode(string Code, IReadOnlyCollection<string> AllowedScopeTypes);
}
