using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.MaterialUnitConversions.GetByMaterial;

internal sealed class GetMaterialUnitConversionsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialUnitConversionsQuery, PagedResult<MaterialUnitConversionResponse>>
{
    public async Task<Result<PagedResult<MaterialUnitConversionResponse>>> Handle(
        GetMaterialUnitConversionsQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<MaterialUnitConversionResponse> conversions = await context.MaterialUnitConversions
            .Where(c => c.MaterialId == query.MaterialId)
            .Select(c => new MaterialUnitConversionResponse
            {
                Id = c.Id,
                MaterialId = c.MaterialId,
                FromUnitId = c.FromUnitId,
                ToBaseUnitId = c.ToBaseUnitId,
                Factor = c.Factor
            })
            .OrderBy(c => c.FromUnitId)
            .ThenBy(c => c.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return conversions;
    }
}
