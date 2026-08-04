using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialUnitConversions.GetByMaterial;

internal sealed class GetMaterialUnitConversionsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialUnitConversionsQuery, List<MaterialUnitConversionResponse>>
{
    public async Task<Result<List<MaterialUnitConversionResponse>>> Handle(
        GetMaterialUnitConversionsQuery query,
        CancellationToken cancellationToken)
    {
        List<MaterialUnitConversionResponse> conversions = await context.MaterialUnitConversions
            .Where(c => c.MaterialId == query.MaterialId)
            .Select(c => new MaterialUnitConversionResponse
            {
                Id = c.Id,
                MaterialId = c.MaterialId,
                FromUnitId = c.FromUnitId,
                ToBaseUnitId = c.ToBaseUnitId,
                Factor = c.Factor
            })
            .ToListAsync(cancellationToken);

        return conversions;
    }
}
