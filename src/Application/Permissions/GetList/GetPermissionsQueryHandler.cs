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
                NameAr = p.NameAr,
                DescriptionAr = p.DescriptionAr,
                Description = p.Description,
                AllowedScopeTypes = context.PermissionAllowedScopeTypes
                    .Where(item => item.PermissionId == p.Id)
                    .OrderBy(item => item.ScopeType)
                    .Select(item => item.ScopeType.ToString())
                    .ToArray()
            })
            .OrderBy(p => p.Code)
            .ThenBy(p => p.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return permissions;
    }
}
