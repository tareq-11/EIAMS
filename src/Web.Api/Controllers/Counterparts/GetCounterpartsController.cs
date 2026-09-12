using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Abstractions.Recipients;
using Application.Abstractions.Authorization;
using Application.Counterparts.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Counterparts;

[ApiController]
[Route("counterparts")]
[Tags(Tags.Counterparts)]
public sealed class GetCounterpartsController(
    IQueryHandler<GetCounterpartsQuery, PagedResult<CounterpartResolution>> handler)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedData<CounterpartResolution>>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    public async Task<IResult> Handle(
        string? search,
        PartyType? type,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetCounterpartsQuery(search, type, pagination.Page, pagination.PageSize);
        Result<PagedResult<CounterpartResolution>> result = await handler.Handle(query, cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
