using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.Materials.GetList;

internal sealed class GetMaterialsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialsQuery, PagedResult<MaterialResponse>>
{
    public async Task<Result<PagedResult<MaterialResponse>>> Handle(
        GetMaterialsQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<MaterialResponse> materials = await (
                from m in context.Materials
                where query.FamilyId == null || m.FamilyId == query.FamilyId
                where query.Status == null || m.Status == query.Status
                join family in context.MaterialFamilies on m.FamilyId equals family.Id
                join category in context.MaterialCategories on family.CategoryId equals category.Id
                join domain in context.MaterialDomains on category.MaterialDomainId equals domain.Id
                join unit in context.UnitsOfMeasure on family.BaseUnitId equals unit.Id
                where query.MaterialDomainId == null || domain.Id == query.MaterialDomainId
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
            .OrderBy(m => m.Code)
            .ThenBy(m => m.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return materials;
    }
}
