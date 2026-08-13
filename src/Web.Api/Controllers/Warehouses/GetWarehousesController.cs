using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Warehouses.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Warehouses;

[ApiController]
[Route("warehouses")]
[Tags(Tags.Warehouses)]
public sealed class GetWarehousesController(IQueryHandler<GetWarehousesQuery, PagedResult<WarehouseResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<WarehouseResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        Guid? siteId,
        Status? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetWarehousesQuery(siteId, status, pagination.Page, pagination.PageSize);

        Result<PagedResult<WarehouseResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
