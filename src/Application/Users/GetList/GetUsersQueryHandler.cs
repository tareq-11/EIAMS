using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Abstractions.Searching;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.GetList;

internal sealed class GetUsersQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetUsersQuery, PagedResult<UserAdministrationResponse>>
{
    public async Task<Result<PagedResult<UserAdministrationResponse>>> Handle(
        GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        Result authorization = await UserAdministrationAuthorization.EnsureEnterpriseAccessAsync(
            scopeAuthorizationService,
            userContext.UserId,
            cancellationToken);

        if (authorization.IsFailure)
        {
            return Result.Failure<PagedResult<UserAdministrationResponse>>(authorization.Error);
        }

        IQueryable<User> users = context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = SqlLikePattern.CreateContains(query.Search, normalizeToUpper: true)!;
#pragma warning disable CA1304, CA1311 // Translated by EF Core to the database UPPER function.
            users = users.Where(user =>
                EF.Functions.Like(user.Email.ToUpper(), term, SqlLikePattern.EscapeCharacter) ||
                EF.Functions.Like(user.FirstName.ToUpper(), term, SqlLikePattern.EscapeCharacter) ||
                EF.Functions.Like(user.LastName.ToUpper(), term, SqlLikePattern.EscapeCharacter));
#pragma warning restore CA1304, CA1311
        }

        if (query.Status.HasValue)
        {
            users = users.Where(user => user.Status == query.Status.Value);
        }

        if (query.HasRoleScope.HasValue)
        {
            users = users.Where(user =>
                context.UserRoleScopes.Any(assignment => assignment.UserId == user.Id) == query.HasRoleScope.Value);
        }

        int totalItems = await users.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);

        var joined =
            from user in users
            join employee in context.Employees.AsNoTracking() on user.EmployeeId equals employee.Id into employees
            from employee in employees.DefaultIfEmpty()
            join assignment in context.UserRoleScopes.AsNoTracking() on user.Id equals assignment.UserId into assignments
            from assignment in assignments.DefaultIfEmpty()
            join role in context.Roles.AsNoTracking() on assignment.RoleId equals role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new { User = user, Employee = employee, Assignment = assignment, Role = role };

#pragma warning disable IDE0031 // Null propagation is not supported in expression trees.
        List<UserAdministrationResponse> items = await joined
            .OrderBy(item => item.User.Email)
            .ThenBy(item => item.User.Id)
            .Skip(offset)
            .Take(query.PageSize)
            .Select(item => new UserAdministrationResponse(
                item.User.Id,
                item.User.Email,
                item.User.FirstName,
                item.User.LastName,
                item.User.EmployeeId,
                item.Employee != null ? item.Employee.FullName : null,
                item.User.Status.ToString(),
                item.User.LastLoginUtc,
                item.User.CreatedAtUtc,
                item.Assignment == null ? null : item.Assignment.RoleId,
                item.Role == null ? null : item.Role.Name,
                item.Assignment == null ? null : item.Assignment.ScopeType.ToString(),
                item.Assignment == null ? null : item.Assignment.ScopeId))
            .ToListAsync(cancellationToken);
#pragma warning restore IDE0031

        return new PagedResult<UserAdministrationResponse>(
            items,
            query.Page,
            query.PageSize,
            totalItems);
    }
}
