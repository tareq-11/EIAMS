using Application.Abstractions.Messaging;

namespace Application.Roles.Update;

public sealed record UpdateRoleCommand(Guid RoleId, string Name, string? Description) : ICommand;
