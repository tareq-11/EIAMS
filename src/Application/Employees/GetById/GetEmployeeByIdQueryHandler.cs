using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Employees.GetById;

internal sealed class GetEmployeeByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    HybridCache hybridCache)
    : IQueryHandler<GetEmployeeByIdQuery, EmployeeResponse>
{
    public async Task<Result<EmployeeResponse>> Handle(GetEmployeeByIdQuery query, CancellationToken cancellationToken)
    {
        Guid? organizationalUnitId = await context.Employees
            .AsNoTracking()
            .Where(employee => employee.Id == query.EmployeeId)
            .Select(employee => (Guid?)employee.OrgUnitId)
            .SingleOrDefaultAsync(cancellationToken);

        bool authorized = organizationalUnitId.HasValue &&
                          await scopeAuthorizationService.HasPermissionAsync(
                              userContext.UserId,
                              PermissionCodes.Employees.View,
                              cancellationToken) &&
                          await scopeAuthorizationService.CanAccessOrganizationalUnitAsync(
                              userContext.UserId,
                              organizationalUnitId.Value,
                              cancellationToken);

        if (!authorized)
        {
            return Result.Failure<EmployeeResponse>(EmployeeErrors.NotFound(query.EmployeeId));
        }

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
