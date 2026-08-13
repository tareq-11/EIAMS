using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.Sites.GetList;

internal sealed class GetSitesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetSitesQuery, PagedResult<SiteResponse>>
{
    public async Task<Result<PagedResult<SiteResponse>>> Handle(
        GetSitesQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<SiteResponse> sites = await context.Sites
            .Where(s => query.OrganizationId == null || s.OrganizationId == query.OrganizationId)
            .Where(s => query.Status == null || s.Status == query.Status)
            .Select(s => new SiteResponse
            {
                Id = s.Id,
                OrganizationId = s.OrganizationId,
                Name = s.Name,
                Code = s.Code,
                Location = s.Location,
                Status = s.Status.ToString()
            })
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return sites;
    }
}
