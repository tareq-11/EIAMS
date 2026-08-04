using Application.Abstractions.Messaging;

namespace Application.RolePermissions.Remove;

public sealed record RemovePermissionFromRoleCommand(Guid RoleId, Guid PermissionId) : ICommand;
