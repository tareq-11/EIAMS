using Application.Abstractions.Messaging;
using Application.Roles;
using Domain.Common;

namespace Application.RolePermissions.Replace;

/// <param name="ExpectedRowVersion">
/// The <c>rowVersion</c> the caller last read from any role projection. Metadata updates and
/// permission replacements advance the same aggregate version, so a client that read the role
/// before someone changed its permissions cannot overwrite that change.
/// </param>
public sealed record ReplaceRolePermissionsCommand(
    Guid RoleId,
    IReadOnlyCollection<string> PermissionCodes,
    int ExpectedRowVersion) : ICommand<RoleResponse>;