using Application.Abstractions.Messaging;

namespace Application.Users.RecoverAdministrator;

public sealed record RecoverAdministratorCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string? RecoveryToken)
    : ICommand<Guid>;
