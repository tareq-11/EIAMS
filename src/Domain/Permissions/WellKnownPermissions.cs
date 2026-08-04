namespace Domain.Permissions;

/// <summary>
/// Fixed ids for permissions seeded via migration (see Infrastructure EF configurations for the seed
/// data itself, and <c>Application.Abstractions.Authorization.PermissionCodes</c> for the string
/// codes). Kept in Domain so any layer can reference a specific seeded permission by id if needed.
/// </summary>
public static class WellKnownPermissions
{
    public static readonly Guid UsersAccessId = Guid.Parse("00000000-0000-0000-0000-000000000101");
    public static readonly Guid OrganizationsManageId = Guid.Parse("00000000-0000-0000-0000-000000000102");
    public static readonly Guid SitesManageId = Guid.Parse("00000000-0000-0000-0000-000000000103");
    public static readonly Guid OrganizationalUnitsManageId = Guid.Parse("00000000-0000-0000-0000-000000000104");
    public static readonly Guid EmployeesManageId = Guid.Parse("00000000-0000-0000-0000-000000000105");
    public static readonly Guid RolesManageId = Guid.Parse("00000000-0000-0000-0000-000000000106");
}
