using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Employees.SetStatus;

public sealed record SetEmployeeStatusCommand(Guid EmployeeId, Status Status) : ICommand;
