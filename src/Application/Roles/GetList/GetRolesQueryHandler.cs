using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.Roles.GetList;

internal sealed class GetRolesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRolesQuery, PagedResult<RoleResponse>>
{
    public async Task<Result<PagedResult<RoleResponse>>> Handle(
        GetRolesQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<RoleResponse> roles = await context.Roles
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description
            })
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return roles;
    }
}
