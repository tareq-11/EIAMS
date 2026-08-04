using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitsOfMeasure.GetById;

internal sealed class GetUnitOfMeasureByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureResponse>
{
    public async Task<Result<UnitOfMeasureResponse>> Handle(
        GetUnitOfMeasureByIdQuery query,
        CancellationToken cancellationToken)
    {
        UnitOfMeasureResponse? unit = await context.UnitsOfMeasure
            .Where(u => u.Id == query.UnitOfMeasureId)
            .Select(u => new UnitOfMeasureResponse
            {
                Id = u.Id,
                Name = u.Name,
                Symbol = u.Symbol,
                UnitType = u.UnitType
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (unit is null)
        {
            return Result.Failure<UnitOfMeasureResponse>(UnitOfMeasureErrors.NotFound(query.UnitOfMeasureId));
        }

        return unit;
    }
}
