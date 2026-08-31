using Application.Abstractions.Messaging;

namespace Application.UserRoleScopes.RemoveAssignment;

public sealed record RemoveUserRoleScopeCommand(Guid UserId) : ICommand;
