using Application.Abstractions.Messaging;

namespace Application.UserRoleScopes.Revoke;

public sealed record RevokeUserRoleScopeCommand(Guid UserRoleScopeId) : ICommand;
