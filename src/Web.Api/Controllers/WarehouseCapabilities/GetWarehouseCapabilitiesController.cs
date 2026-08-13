using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.WarehouseCapabilities.GetByWarehouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseCapabilities;

[ApiController]
[Route("warehouses")]
[Tags(Tags.WarehouseCapabilities)]
public sealed class GetWarehouseCapabilitiesController(
    IQueryHandler<GetWarehouseCapabilitiesQuery, PagedResult<WarehouseCapabilityResponse>> handler)
    : ControllerBase
{
    [HttpGet("{warehouseId:guid}/capabilities")]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<WarehouseCapabilityResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        Guid warehouseId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetWarehouseCapabilitiesQuery(warehouseId, pagination.Page, pagination.PageSize);

        Result<PagedResult<WarehouseCapabilityResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
