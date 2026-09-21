using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Authorization;

internal sealed class AnyPermissionRequirement(IReadOnlyCollection<string> permissions) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> Permissions { get; } = permissions;
}
