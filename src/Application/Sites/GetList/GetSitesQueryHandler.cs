using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.Sites.GetList;

internal sealed class GetSitesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetSitesQuery, PagedResult<SiteResponse>>
{
    public async Task<Result<PagedResult<SiteResponse>>> Handle(
        GetSitesQuery query,
        CancellationToken cancellationToken)
    {
        SitePermissionScope permissionScope = await scopeAuthorizationService.GetSitePermissionScopeAsync(
            userContext.UserId,
            PermissionCodes.Sites.View,
            cancellationToken);

        PagedResult<SiteResponse> sites = await context.Sites
            .Where(site => permissionScope.HasEnterpriseAccess || permissionScope.SiteIds.Contains(site.Id))
            .Where(s => query.OrganizationId == null || s.OrganizationId == query.OrganizationId)
            .Where(s => query.Status == null || s.Status == query.Status)
            .Select(s => new SiteResponse
            {
                Id = s.Id,
                OrganizationId = s.OrganizationId,
                Name = s.Name,
                Code = s.Code,
                Location = s.Location,
                GovernorateCode = s.GovernorateCode,
                Status = s.Status.ToString()
            })
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return sites;
    }
}
