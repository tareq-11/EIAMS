using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.InventoryBalances.GetList;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryBalances;

public sealed record GetInventoryBalancesRequest(Guid? WarehouseId, Guid? MaterialId, string? Search);

[ApiController]
[Route("inventory/balances")]
[Tags(Tags.InventoryLedger)]
public sealed class GetInventoryBalancesController(
    IQueryHandler<GetInventoryBalancesQuery, PagedResult<InventoryBalanceResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<InventoryBalanceResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] GetInventoryBalancesRequest request,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetInventoryBalancesQuery(
            request.WarehouseId,
            request.MaterialId,
            request.Search,
            pagination.Page,
            pagination.PageSize);

        Result<PagedResult<InventoryBalanceResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
