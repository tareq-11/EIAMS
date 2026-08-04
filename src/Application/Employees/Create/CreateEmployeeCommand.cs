using Application.Abstractions.Messaging;

namespace Application.Employees.Create;

public sealed record CreateEmployeeCommand(Guid OrgUnitId, string FullName, string EmployeeNumber, string? JobTitle)
    : ICommand<Guid>;
