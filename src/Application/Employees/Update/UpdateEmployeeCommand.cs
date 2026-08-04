using Application.Abstractions.Messaging;

namespace Application.Employees.Update;

public sealed record UpdateEmployeeCommand(Guid EmployeeId, string FullName, string? JobTitle) : ICommand;
