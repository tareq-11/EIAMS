using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.MaterialUnitConversions.GetByMaterial;
using Domain.MaterialUnitConversions;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialUnitConversions.GetById;

internal sealed class GetMaterialUnitConversionByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialUnitConversionByIdQuery, MaterialUnitConversionResponse>
{
    public async Task<Result<MaterialUnitConversionResponse>> Handle(
        GetMaterialUnitConversionByIdQuery query,
        CancellationToken cancellationToken)
    {
        MaterialUnitConversionResponse? conversion = await context.MaterialUnitConversions
            .AsNoTracking()
            .Where(c => c.MaterialId == query.MaterialId && c.Id == query.ConversionId)
            .Select(c => new MaterialUnitConversionResponse
            {
                Id = c.Id,
                MaterialId = c.MaterialId,
                FromUnitId = c.FromUnitId,
                ToBaseUnitId = c.ToBaseUnitId,
                Factor = c.Factor
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (conversion is null)
        {
            return Result.Failure<MaterialUnitConversionResponse>(
                MaterialUnitConversionErrors.NotFound(query.ConversionId));
        }

        return conversion;
    }
}
