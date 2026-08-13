using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.Employees.GetList;

public sealed record GetEmployeesQuery(Guid? OrgUnitId, Status? Status, int Page, int PageSize)
    : IQuery<PagedResult<EmployeeResponse>>;
