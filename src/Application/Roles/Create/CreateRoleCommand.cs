using Application.Abstractions.Messaging;
using Application.Roles;
using Domain.Common;

namespace Application.Roles.Create;

/// <param name="PermissionCodes">
/// The role's initial complete permission-code set (RESOLUTION-027 S16). It is required so a role
/// is never persisted with an empty or partial grant set that an administrator must remember to
/// finish; role and grants are created in one transaction.
/// </param>
public sealed record CreateRoleCommand(
    string Name,
    string NameAr,
    string? Description,
    IReadOnlyCollection<string> PermissionCodes,
    IReadOnlyCollection<ScopeType>? AllowedScopeTypes = null) : ICommand<RoleResponse>;