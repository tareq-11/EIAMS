namespace Application.Abstractions.Authorization;

/// <summary>
/// Canonical permission codes. Defined in Application (not Web.Api) because handlers need the same
/// string as the endpoint's <c>.HasPermission(...)</c> gate for the finer-grained
/// <see cref="IScopeAuthorizationService"/> check - duplicating the literal in both layers invites drift.
/// </summary>
public static class PermissionCodes
{
    public static class Organizations
    {
        public const string Manage = "organizations:manage";
    }

    public static class Sites
    {
        public const string Manage = "sites:manage";
    }

    public static class OrganizationalUnits
    {
        public const string Manage = "org-units:manage";
    }

    public static class Employees
    {
        public const string Manage = "employees:manage";
    }

    public static class Roles
    {
        public const string Manage = "roles:manage";
    }
}
