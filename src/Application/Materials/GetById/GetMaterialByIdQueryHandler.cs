using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Materials.GetById;

internal sealed class GetMaterialByIdQueryHandler(
    IApplicationDbContext context,
    HybridCache hybridCache)
    : IQueryHandler<GetMaterialByIdQuery, MaterialResponse>
{
    public async Task<Result<MaterialResponse>> Handle(GetMaterialByIdQuery query, CancellationToken cancellationToken)
    {
        MaterialResponse? material = await hybridCache.GetOrCreateAsync(
            $"materials:by-id:{query.MaterialId}",
            async ct => await (
                from m in context.Materials.AsNoTracking()
                where m.Id == query.MaterialId
                join family in context.MaterialFamilies.AsNoTracking() on m.FamilyId equals family.Id
                join category in context.MaterialCategories.AsNoTracking() on family.CategoryId equals category.Id
                join domain in context.MaterialDomains.AsNoTracking() on category.MaterialDomainId equals domain.Id
                join unit in context.UnitsOfMeasure.AsNoTracking() on family.BaseUnitId equals unit.Id
                select new MaterialResponse
                {
                    Id = m.Id,
                    FamilyId = m.FamilyId,
                    NameAr = m.NameAr,
                    NameEn = m.NameEn,
                    Code = m.Code,
                    MaterialKind = m.MaterialKind.ToString(),
                    TrackingType = m.TrackingType.ToString(),
                    HasExpiry = m.HasExpiry,
                    RequiresAssetNumber = m.RequiresAssetNumber,
                    Attributes = m.Attributes,
                    Status = m.Status.ToString(),
                    MaterialDomainId = domain.Id,
                    MaterialDomainName = domain.Name,
                    BaseUnitId = unit.Id,
                    BaseUnitSymbol = unit.Symbol
                })
                .SingleOrDefaultAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: ["materials"],
            cancellationToken: cancellationToken);

        if (material is null)
        {
            return Result.Failure<MaterialResponse>(MaterialErrors.NotFound(query.MaterialId));
        }

        return material;
    }
}
