using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.Employees.GetList;

internal sealed class GetEmployeesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetEmployeesQuery, PagedResult<EmployeeResponse>>
{
    public async Task<Result<PagedResult<EmployeeResponse>>> Handle(
        GetEmployeesQuery query,
        CancellationToken cancellationToken)
    {
        OrganizationalUnitPermissionScope permissionScope =
            await scopeAuthorizationService.GetOrganizationalUnitPermissionScopeAsync(
                userContext.UserId,
                PermissionCodes.Employees.View,
                cancellationToken);

        PagedResult<EmployeeResponse> employees = await context.Employees
            .Where(employee => permissionScope.HasEnterpriseAccess ||
                               permissionScope.OrganizationalUnitIds.Contains(employee.OrgUnitId))
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
            .OrderBy(e => e.FullName)
            .ThenBy(e => e.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return employees;
    }
}
