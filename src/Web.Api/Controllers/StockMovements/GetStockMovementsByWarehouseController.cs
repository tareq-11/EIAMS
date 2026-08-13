using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.StockMovements.GetByWarehouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.StockMovements;

[ApiController]
[Route("warehouses/{warehouseId:guid}/stock-movements")]
[Tags(Tags.InventoryLedger)]
public sealed class GetStockMovementsByWarehouseController(
    IQueryHandler<GetStockMovementsByWarehouseQuery, PagedResult<StockMovementResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<StockMovementResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        Guid warehouseId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetStockMovementsByWarehouseQuery(warehouseId, pagination.Page, pagination.PageSize);

        Result<PagedResult<StockMovementResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
