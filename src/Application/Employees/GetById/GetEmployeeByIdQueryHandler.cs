using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Employees;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Employees.GetById;

internal sealed class GetEmployeeByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetEmployeeByIdQuery, EmployeeResponse>
{
    public async Task<Result<EmployeeResponse>> Handle(GetEmployeeByIdQuery query, CancellationToken cancellationToken)
    {
        EmployeeResponse? employee = await context.Employees
            .Where(e => e.Id == query.EmployeeId)
            .Select(e => new EmployeeResponse
            {
                Id = e.Id,
                OrgUnitId = e.OrgUnitId,
                FullName = e.FullName,
                EmployeeNumber = e.EmployeeNumber,
                JobTitle = e.JobTitle,
                Status = e.Status.ToString()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return Result.Failure<EmployeeResponse>(EmployeeErrors.NotFound(query.EmployeeId));
        }

        return employee;
    }
}
