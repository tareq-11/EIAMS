using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Employees;
using Domain.OrganizationalUnits;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Employees.Update;

internal sealed class UpdateEmployeeCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<UpdateEmployeeCommand>
{
    public async Task<Result> Handle(UpdateEmployeeCommand command, CancellationToken cancellationToken)
    {
        Employee? employee = await context.Employees
            .SingleOrDefaultAsync(e => e.Id == command.EmployeeId, cancellationToken);

        if (employee is null)
        {
            return Result.Failure(EmployeeErrors.NotFound(command.EmployeeId));
        }

        OrganizationalUnit? orgUnit = await context.OrganizationalUnits
            .SingleOrDefaultAsync(u => u.Id == employee.OrgUnitId, cancellationToken);

        if (orgUnit is null)
        {
            return Result.Failure(EmployeeErrors.OrganizationalUnitNotFound(employee.OrgUnitId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Employees.Manage,
            ScopeType.Site,
            orgUnit.SiteId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(EmployeeErrors.Forbidden);
        }

        employee.UpdateDetails(command.FullName, command.JobTitle);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
