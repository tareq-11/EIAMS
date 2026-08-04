using Application.Abstractions.Messaging;

namespace Application.Roles.Create;

public sealed record CreateRoleCommand(string Name, string? Description) : ICommand<Guid>;
