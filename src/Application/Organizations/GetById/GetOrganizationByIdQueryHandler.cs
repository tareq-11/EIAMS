using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Organizations.GetById;

internal sealed class GetOrganizationByIdQueryHandler(
    IApplicationDbContext context,
    HybridCache hybridCache)
    : IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse>
{
    public async Task<Result<OrganizationResponse>> Handle(
        GetOrganizationByIdQuery query,
        CancellationToken cancellationToken)
    {
        OrganizationResponse? organization = await hybridCache.GetOrCreateAsync(
            $"organizations:by-id:{query.OrganizationId}",
            async ct => await context.Organizations
                .AsNoTracking()
                .Where(o => o.Id == query.OrganizationId)
                .Select(o => new OrganizationResponse
                {
                    Id = o.Id,
                    Name = o.Name,
                    Code = o.Code,
                    Status = o.Status.ToString()
                })
                .SingleOrDefaultAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: ["organizations"],
            cancellationToken: cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationResponse>(OrganizationErrors.NotFound(query.OrganizationId));
        }

        return organization;
    }
}
