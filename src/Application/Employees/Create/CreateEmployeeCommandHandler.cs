using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Employees;
using Domain.OrganizationalUnits;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Employees.Create;

internal sealed class CreateEmployeeCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<CreateEmployeeCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateEmployeeCommand command, CancellationToken cancellationToken)
    {
        OrganizationalUnit? orgUnit = await context.OrganizationalUnits
            .SingleOrDefaultAsync(u => u.Id == command.OrgUnitId, cancellationToken);

        if (orgUnit is null)
        {
            return Result.Failure<Guid>(EmployeeErrors.OrganizationalUnitNotFound(command.OrgUnitId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Employees.Manage,
            ScopeType.Site,
            orgUnit.SiteId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(EmployeeErrors.Forbidden);
        }

        if (await context.Employees.AnyAsync(e => e.EmployeeNumber == command.EmployeeNumber, cancellationToken))
        {
            return Result.Failure<Guid>(EmployeeErrors.EmployeeNumberNotUnique);
        }

        var employee = Employee.Create(
            Guid.NewGuid(),
            command.OrgUnitId,
            command.FullName,
            command.EmployeeNumber,
            command.JobTitle);

        context.Employees.Add(employee);

        await context.SaveChangesAsync(cancellationToken);

        return employee.Id;
    }
}
