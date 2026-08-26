using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MaterialCategories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.MaterialCategories.GetById;

internal sealed class GetMaterialCategoryByIdQueryHandler(
    IApplicationDbContext context,
    HybridCache hybridCache)
    : IQueryHandler<GetMaterialCategoryByIdQuery, MaterialCategoryResponse>
{
    public async Task<Result<MaterialCategoryResponse>> Handle(
        GetMaterialCategoryByIdQuery query,
        CancellationToken cancellationToken)
    {
        MaterialCategoryResponse? category = await hybridCache.GetOrCreateAsync(
            $"categories:by-id:{query.MaterialCategoryId}",
            async ct => await context.MaterialCategories
                .AsNoTracking()
                .Where(c => c.Id == query.MaterialCategoryId)
                .Select(c => new MaterialCategoryResponse
                {
                    Id = c.Id,
                    MaterialDomainId = c.MaterialDomainId,
                    ParentCategoryId = c.ParentCategoryId,
                    Name = c.Name,
                    Code = c.Code,
                    Status = c.Status.ToString()
                })
                .SingleOrDefaultAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: ["categories"],
            cancellationToken: cancellationToken);

        if (category is null)
        {
            return Result.Failure<MaterialCategoryResponse>(MaterialCategoryErrors.NotFound(query.MaterialCategoryId));
        }

        return category;
    }
}
