using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.UserRoleScopes.Grant;

public sealed record GrantUserRoleScopeCommand(Guid UserId, Guid RoleId, ScopeType ScopeType, Guid? ScopeId)
    : ICommand<Guid>;
