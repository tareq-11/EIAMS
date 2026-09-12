using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.StockMovements.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.StockMovements;

public sealed record GetStockMovementsRequest(
    Guid? WarehouseId,
    Guid? MaterialId,
    Guid? DocumentId,
    MovementType? MovementType,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Search);

[ApiController]
[Route("inventory/movements")]
[Tags(Tags.InventoryLedger)]
public sealed class GetStockMovementsController(
    IQueryHandler<GetStockMovementsQuery, PagedResult<StockMovementResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType<ApiResponse<PagedData<StockMovementResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] GetStockMovementsRequest request,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetStockMovementsQuery(
            request.WarehouseId,
            request.MaterialId,
            request.DocumentId,
            request.MovementType,
            request.FromUtc,
            request.ToUtc,
            request.Search,
            pagination.Page,
            pagination.PageSize);

        Result<PagedResult<StockMovementResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
