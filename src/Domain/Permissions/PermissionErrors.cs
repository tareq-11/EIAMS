using SharedKernel;

namespace Domain.Permissions;

public static class PermissionErrors
{
    public static Error NotFound(Guid permissionId) => Error.NotFound(
        "Permissions.NotFound",
        $"The permission with the Id = '{permissionId}' was not found");
}
