using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Employees;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.LinkEmployee;

internal sealed class LinkUserToEmployeeCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<LinkUserToEmployeeCommand>
{
    public async Task<Result> Handle(LinkUserToEmployeeCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        Guid? siteId = await (
                from employee in context.Employees
                where employee.Id == command.EmployeeId
                join orgUnit in context.OrganizationalUnits on employee.OrgUnitId equals orgUnit.Id
                select (Guid?)orgUnit.SiteId)
            .SingleOrDefaultAsync(cancellationToken);

        if (siteId is null)
        {
            return Result.Failure(EmployeeErrors.NotFound(command.EmployeeId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Employees.Manage,
            ScopeType.Site,
            siteId.Value,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(UserErrors.Unauthorized());
        }

        bool alreadyLinked = await context.Users.AnyAsync(
            candidate => candidate.Id != command.UserId && candidate.EmployeeId == command.EmployeeId,
            cancellationToken);

        if (alreadyLinked)
        {
            return Result.Failure(UserErrors.EmployeeAlreadyLinked);
        }

        user.LinkToEmployee(command.EmployeeId);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
