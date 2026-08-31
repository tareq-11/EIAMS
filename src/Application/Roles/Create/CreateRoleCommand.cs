using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Roles.Create;

public sealed record CreateRoleCommand(
    string Name,
    string? Description,
    IReadOnlyCollection<ScopeType>? AllowedScopeTypes = null) : ICommand<Guid>;
