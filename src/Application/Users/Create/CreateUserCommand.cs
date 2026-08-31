using Application.Abstractions.Messaging;

namespace Application.Users.Create;

public sealed record CreateUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password) : ICommand<Guid>;
