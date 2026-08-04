using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sites.GetList;

internal sealed class GetSitesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetSitesQuery, List<SiteResponse>>
{
    public async Task<Result<List<SiteResponse>>> Handle(GetSitesQuery query, CancellationToken cancellationToken)
    {
        List<SiteResponse> sites = await context.Sites
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
            .ToListAsync(cancellationToken);

        return sites;
    }
}
