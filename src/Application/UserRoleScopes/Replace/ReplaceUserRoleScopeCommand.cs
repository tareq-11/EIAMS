using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.UserRoleScopes.Replace;

public sealed record ReplaceUserRoleScopeCommand(
    Guid UserId,
    Guid RoleId,
    ScopeType ScopeType,
    Guid? ScopeId) : ICommand<Guid>;
