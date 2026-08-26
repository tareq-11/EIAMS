using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MaterialDomains;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.MaterialDomains.GetById;

internal sealed class GetMaterialDomainByIdQueryHandler(
    IApplicationDbContext context,
    HybridCache hybridCache)
    : IQueryHandler<GetMaterialDomainByIdQuery, MaterialDomainResponse>
{
    public async Task<Result<MaterialDomainResponse>> Handle(
        GetMaterialDomainByIdQuery query,
        CancellationToken cancellationToken)
    {
        MaterialDomainResponse? materialDomain = await hybridCache.GetOrCreateAsync(
            $"domains:by-id:{query.MaterialDomainId}",
            async ct => await context.MaterialDomains
                .AsNoTracking()
                .Where(d => d.Id == query.MaterialDomainId)
                .Select(d => new MaterialDomainResponse
                {
                    Id = d.Id,
                    Name = d.Name,
                    Code = d.Code,
                    Status = d.Status.ToString()
                })
                .SingleOrDefaultAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: ["domains"],
            cancellationToken: cancellationToken);

        if (materialDomain is null)
        {
            return Result.Failure<MaterialDomainResponse>(MaterialDomainErrors.NotFound(query.MaterialDomainId));
        }

        return materialDomain;
    }
}
