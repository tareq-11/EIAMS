using Application.UserRoleScopes.GetByUser;

namespace Application.Users.Create;

public sealed record CreateUserResponse(
    Guid Id,
    string Email,
    string Username,
    string FirstName,
    string LastName,
    UserRoleScopeResponse Assignment);
