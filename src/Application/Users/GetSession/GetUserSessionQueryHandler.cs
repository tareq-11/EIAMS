using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.GetSession;

internal sealed class GetUserSessionQueryHandler(
    IApplicationDbContext context,
    IEffectivePermissionService effectivePermissionService) : IQueryHandler<GetUserSessionQuery, UserSessionResponse>
{
    public async Task<Result<UserSessionResponse>> Handle(
        GetUserSessionQuery query,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == query.UserId)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.EmployeeId,
                u.Status
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserSessionResponse>(UserErrors.NotFound(query.UserId));
        }

        if (user.Status == UserStatus.Suspended)
        {
            return Result.Failure<UserSessionResponse>(UserErrors.Suspended);
        }

        string? employeeName = null;
        if (user.EmployeeId.HasValue)
        {
            employeeName = await context.Employees
                .AsNoTracking()
                .Where(emp => emp.Id == user.EmployeeId.Value)
                .Select(emp => emp.FullName)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var assignments = await (
            from userRoleScope in context.UserRoleScopes.AsNoTracking()
            where userRoleScope.UserId == query.UserId &&
                  userRoleScope.ScopeType != ScopeType.OrganizationalUnit
            join role in context.Roles.AsNoTracking() on userRoleScope.RoleId equals role.Id
            select new
            {
                Assignment = userRoleScope,
                Role = role
            })
            .Take(2)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return Result.Failure<UserSessionResponse>(UserRoleScopeErrors.NoAssignment(query.UserId));
        }

        if (assignments.Count > 1)
        {
            return Result.Failure<UserSessionResponse>(UserRoleScopeErrors.MultipleAssignments(query.UserId));
        }

        var assignment = assignments[0];

        string scopeName = assignment.Assignment.ScopeType switch
        {
            ScopeType.Enterprise => "Enterprise",
            ScopeType.Site when assignment.Assignment.ScopeId.HasValue => await context.Sites
                .AsNoTracking()
                .Where(s => s.Id == assignment.Assignment.ScopeId.Value)
                .Select(s => s.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? "Unknown Site",
            ScopeType.Warehouse when assignment.Assignment.ScopeId.HasValue => await context.Warehouses
                .AsNoTracking()
                .Where(w => w.Id == assignment.Assignment.ScopeId.Value)
                .Select(w => w.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? "Unknown Warehouse",
            _ => "Unknown Scope"
        };

        IReadOnlyList<string> permissionCodes = await effectivePermissionService
            .GetEffectivePermissionCodesAsync(query.UserId, cancellationToken);

        return new UserSessionResponse(
            new UserSessionUserDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.EmployeeId,
                employeeName),
            new UserSessionRoleDto(
                assignment.Role.Id,
                assignment.Role.Name,
                assignment.Role.NameAr,
                assignment.Role.Description),
            new UserSessionScopeDto(
                assignment.Assignment.ScopeType.ToAssignmentScopeType(),
                assignment.Assignment.ScopeId,
                scopeName),
            permissionCodes);
    }
}
