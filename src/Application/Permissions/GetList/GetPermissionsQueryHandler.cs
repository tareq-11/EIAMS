using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.Permissions.GetList;

internal sealed class GetPermissionsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetPermissionsQuery, PagedResult<PermissionResponse>>
{
    public async Task<Result<PagedResult<PermissionResponse>>> Handle(
        GetPermissionsQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<PermissionResponse> permissions = await context.Permissions
            .Select(p => new PermissionResponse
            {
                Id = p.Id,
                Code = p.Code,
                Description = p.Description
            })
            .OrderBy(p => p.Code)
            .ThenBy(p => p.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return permissions;
    }
}
