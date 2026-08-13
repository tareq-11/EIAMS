using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.InventoryBalances.GetByWarehouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryBalances;

[ApiController]
[Route("warehouses/{warehouseId:guid}/balances")]
[Tags(Tags.InventoryLedger)]
public sealed class GetInventoryBalancesByWarehouseController(
    IQueryHandler<GetInventoryBalancesByWarehouseQuery, PagedResult<InventoryBalanceResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<InventoryBalanceResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        Guid warehouseId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetInventoryBalancesByWarehouseQuery(warehouseId, pagination.Page, pagination.PageSize);

        Result<PagedResult<InventoryBalanceResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
