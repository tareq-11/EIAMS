using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Materials.GetById;

internal sealed class GetMaterialByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialByIdQuery, MaterialResponse>
{
    public async Task<Result<MaterialResponse>> Handle(GetMaterialByIdQuery query, CancellationToken cancellationToken)
    {
        MaterialResponse? material = await (
                from m in context.Materials
                where m.Id == query.MaterialId
                join family in context.MaterialFamilies on m.FamilyId equals family.Id
                join category in context.MaterialCategories on family.CategoryId equals category.Id
                join domain in context.MaterialDomains on category.MaterialDomainId equals domain.Id
                join unit in context.UnitsOfMeasure on family.BaseUnitId equals unit.Id
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
            .SingleOrDefaultAsync(cancellationToken);

        if (material is null)
        {
            return Result.Failure<MaterialResponse>(MaterialErrors.NotFound(query.MaterialId));
        }

        return material;
    }
}
