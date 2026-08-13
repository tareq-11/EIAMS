using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.UnitsOfMeasure.GetList;

internal sealed class GetUnitsOfMeasureQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetUnitsOfMeasureQuery, PagedResult<UnitOfMeasureResponse>>
{
    public async Task<Result<PagedResult<UnitOfMeasureResponse>>> Handle(
        GetUnitsOfMeasureQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<UnitOfMeasureResponse> units = await context.UnitsOfMeasure
            .Select(u => new UnitOfMeasureResponse
            {
                Id = u.Id,
                Name = u.Name,
                Symbol = u.Symbol,
                UnitType = u.UnitType
            })
            .OrderBy(u => u.Name)
            .ThenBy(u => u.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return units;
    }
}
