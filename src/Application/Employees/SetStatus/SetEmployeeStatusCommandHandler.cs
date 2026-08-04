using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Employees;
using Domain.OrganizationalUnits;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Employees.SetStatus;

internal sealed class SetEmployeeStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<SetEmployeeStatusCommand>
{
    public async Task<Result> Handle(SetEmployeeStatusCommand command, CancellationToken cancellationToken)
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

        employee.SetStatus(command.Status);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
