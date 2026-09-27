using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Users.Create;

public sealed record CreateUserCommand(
    string Email,
    string Username,
    string FirstName,
    string LastName,
    string Password,
    Guid RoleId,
    UserAssignmentScopeType ScopeType,
    Guid? ScopeId) : ICommand<CreateUserResponse>;
