using Application.Abstractions.Messaging;

namespace Application.Users.LinkEmployee;

public sealed record LinkUserToEmployeeCommand(Guid UserId, Guid EmployeeId) : ICommand;
