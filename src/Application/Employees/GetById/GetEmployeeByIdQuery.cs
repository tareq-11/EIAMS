using Application.Abstractions.Messaging;

namespace Application.Employees.GetById;

public sealed record GetEmployeeByIdQuery(Guid EmployeeId) : IQuery<EmployeeResponse>;
