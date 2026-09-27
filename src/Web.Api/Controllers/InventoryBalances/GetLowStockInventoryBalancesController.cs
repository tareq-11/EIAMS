using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.InventoryBalances.GetLowStock;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryBalances;

[ApiController]
[Route("inventory/balances")]
[Tags(Tags.InventoryLedger)]
public sealed class GetLowStockInventoryBalancesController(
    IQueryHandler<GetLowStockInventoryBalancesQuery, PagedResult<LowStockInventoryBalanceResponse>> handler)
    : ControllerBase
{
    [HttpGet("low-stock")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LowStockInventoryBalanceResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] Guid? warehouseId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetLowStockInventoryBalancesQuery(warehouseId, pagination.Page, pagination.PageSize);
        Result<PagedResult<LowStockInventoryBalanceResponse>> result = await handler.Handle(query, cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
