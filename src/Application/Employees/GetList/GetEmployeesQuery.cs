using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Employees.GetList;

public sealed record GetEmployeesQuery(Guid? OrgUnitId, Status? Status) : IQuery<List<EmployeeResponse>>;
