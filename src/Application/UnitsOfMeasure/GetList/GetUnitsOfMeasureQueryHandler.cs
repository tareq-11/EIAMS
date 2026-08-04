using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitsOfMeasure.GetList;

internal sealed class GetUnitsOfMeasureQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetUnitsOfMeasureQuery, List<UnitOfMeasureResponse>>
{
    public async Task<Result<List<UnitOfMeasureResponse>>> Handle(
        GetUnitsOfMeasureQuery query,
        CancellationToken cancellationToken)
    {
        List<UnitOfMeasureResponse> units = await context.UnitsOfMeasure
            .Select(u => new UnitOfMeasureResponse
            {
                Id = u.Id,
                Name = u.Name,
                Symbol = u.Symbol,
                UnitType = u.UnitType
            })
            .ToListAsync(cancellationToken);

        return units;
    }
}
