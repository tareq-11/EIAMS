using Application.Abstractions.Messaging;

namespace Application.Users.Logout;

public sealed record LogoutUserCommand(string? RefreshToken) : ICommand;
