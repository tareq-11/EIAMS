using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.GetById;

internal sealed class GetUserByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        Result authorization = await UserAdministrationAuthorization.EnsureEnterpriseAccessAsync(
            scopeAuthorizationService,
            userContext.UserId,
            cancellationToken);

        if (authorization.IsFailure)
        {
            return Result.Failure<UserResponse>(authorization.Error);
        }

#pragma warning disable IDE0031 // Null propagation is not supported in expression trees.
        UserResponse? user = await (
                from account in context.Users.AsNoTracking()
                where account.Id == query.UserId
                join employee in context.Employees.AsNoTracking()
                    on account.EmployeeId equals employee.Id into employees
                from employee in employees.DefaultIfEmpty()
                select new UserResponse
                {
                    Id = account.Id,
                    FirstName = account.FirstName,
                    LastName = account.LastName,
                    Email = account.Email,
                    EmployeeId = account.EmployeeId,
                    EmployeeName = employee == null ? null : employee.FullName,
                    Status = account.Status.ToString(),
                    LastLoginUtc = account.LastLoginUtc,
                    CreatedAtUtc = account.CreatedAtUtc
                })
            .SingleOrDefaultAsync(cancellationToken);
#pragma warning restore IDE0031

        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound(query.UserId));
        }

        return user;
    }
}
