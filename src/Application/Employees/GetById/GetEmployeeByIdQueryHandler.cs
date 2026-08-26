using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Employees.GetById;

internal sealed class GetEmployeeByIdQueryHandler(
    IApplicationDbContext context,
    HybridCache hybridCache)
    : IQueryHandler<GetEmployeeByIdQuery, EmployeeResponse>
{
    public async Task<Result<EmployeeResponse>> Handle(GetEmployeeByIdQuery query, CancellationToken cancellationToken)
    {
        EmployeeResponse? employee = await hybridCache.GetOrCreateAsync(
            $"employees:by-id:{query.EmployeeId}",
            async ct => await context.Employees
                .AsNoTracking()
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
                .SingleOrDefaultAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: ["employees"],
            cancellationToken: cancellationToken);

        if (employee is null)
        {
            return Result.Failure<EmployeeResponse>(EmployeeErrors.NotFound(query.EmployeeId));
        }

        return employee;
    }
}
