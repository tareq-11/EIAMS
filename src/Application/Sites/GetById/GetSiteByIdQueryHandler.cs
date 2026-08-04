using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Sites;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sites.GetById;

internal sealed class GetSiteByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetSiteByIdQuery, SiteResponse>
{
    public async Task<Result<SiteResponse>> Handle(GetSiteByIdQuery query, CancellationToken cancellationToken)
    {
        SiteResponse? site = await context.Sites
            .Where(s => s.Id == query.SiteId)
            .Select(s => new SiteResponse
            {
                Id = s.Id,
                OrganizationId = s.OrganizationId,
                Name = s.Name,
                Code = s.Code,
                Location = s.Location,
                Status = s.Status.ToString()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (site is null)
        {
            return Result.Failure<SiteResponse>(SiteErrors.NotFound(query.SiteId));
        }

        return site;
    }
}
