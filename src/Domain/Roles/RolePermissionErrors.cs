using SharedKernel;

namespace Domain.Roles;

public static class RolePermissionErrors
{
    public static readonly Error AlreadyAssigned = Error.Conflict(
        "RolePermissions.AlreadyAssigned",
        "The permission is already assigned to the role");

    public static readonly Error NotAssigned = Error.NotFound(
        "RolePermissions.NotAssigned",
        "The permission is not assigned to the role");
}
