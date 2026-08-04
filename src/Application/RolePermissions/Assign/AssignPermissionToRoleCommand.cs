using Application.Abstractions.Messaging;

namespace Application.RolePermissions.Assign;

public sealed record AssignPermissionToRoleCommand(Guid RoleId, Guid PermissionId) : ICommand;
