using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.UnitsOfMeasure.GetList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UnitsOfMeasure;

[ApiController]
[Route("units-of-measure")]
[Tags(Tags.UnitsOfMeasure)]
public sealed class GetListController(IQueryHandler<GetUnitsOfMeasureQuery, PagedResult<UnitOfMeasureResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<UnitOfMeasureResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetUnitsOfMeasureQuery(pagination.Page, pagination.PageSize);

        Result<PagedResult<UnitOfMeasureResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
