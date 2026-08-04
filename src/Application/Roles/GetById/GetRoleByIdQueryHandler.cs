using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.GetById;

internal sealed class GetRoleByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRoleByIdQuery, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(GetRoleByIdQuery query, CancellationToken cancellationToken)
    {
        RoleResponse? role = await context.Roles
            .Where(r => r.Id == query.RoleId)
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (role is null)
        {
            return Result.Failure<RoleResponse>(RoleErrors.NotFound(query.RoleId));
        }

        return role;
    }
}
