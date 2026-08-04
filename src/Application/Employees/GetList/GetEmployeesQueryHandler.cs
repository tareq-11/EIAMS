using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Employees.GetList;

internal sealed class GetEmployeesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetEmployeesQuery, List<EmployeeResponse>>
{
    public async Task<Result<List<EmployeeResponse>>> Handle(
        GetEmployeesQuery query,
        CancellationToken cancellationToken)
    {
        List<EmployeeResponse> employees = await context.Employees
            .Where(e => query.OrgUnitId == null || e.OrgUnitId == query.OrgUnitId)
            .Where(e => query.Status == null || e.Status == query.Status)
            .Select(e => new EmployeeResponse
            {
                Id = e.Id,
                OrgUnitId = e.OrgUnitId,
                FullName = e.FullName,
                EmployeeNumber = e.EmployeeNumber,
                JobTitle = e.JobTitle,
                Status = e.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return employees;
    }
}
