using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.UnitsOfMeasure.GetList;

public sealed record GetUnitsOfMeasureQuery(int Page, int PageSize) : IQuery<PagedResult<UnitOfMeasureResponse>>;
