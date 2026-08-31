using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Roles.Update;

public sealed record UpdateRoleCommand(
    Guid RoleId,
    string Name,
    string? Description,
    IReadOnlyCollection<ScopeType>? AllowedScopeTypes = null) : ICommand;
