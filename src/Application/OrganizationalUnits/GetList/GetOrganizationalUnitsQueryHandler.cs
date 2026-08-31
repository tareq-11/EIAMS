using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.OrganizationalUnits.GetList;

internal sealed class GetOrganizationalUnitsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetOrganizationalUnitsQuery, PagedResult<OrganizationalUnitResponse>>
{
    public async Task<Result<PagedResult<OrganizationalUnitResponse>>> Handle(
        GetOrganizationalUnitsQuery query,
        CancellationToken cancellationToken)
    {
        OrganizationalUnitPermissionScope permissionScope =
            await scopeAuthorizationService.GetOrganizationalUnitPermissionScopeAsync(
                userContext.UserId,
                PermissionCodes.OrganizationalUnits.View,
                cancellationToken);

        PagedResult<OrganizationalUnitResponse> units = await context.OrganizationalUnits
            .Where(unit => permissionScope.HasEnterpriseAccess ||
                           permissionScope.OrganizationalUnitIds.Contains(unit.Id))
            .Where(u => query.SiteId == null || u.SiteId == query.SiteId)
            .Where(u => query.ParentId == null || u.ParentId == query.ParentId)
            .Where(u => query.Status == null || u.Status == query.Status)
            .Select(u => new OrganizationalUnitResponse
            {
                Id = u.Id,
                SiteId = u.SiteId,
                ParentId = u.ParentId,
                Name = u.Name,
                UnitType = u.UnitType,
                Status = u.Status.ToString()
            })
            .OrderBy(u => u.Name)
            .ThenBy(u => u.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return units;
    }
}
