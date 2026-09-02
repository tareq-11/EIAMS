using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.StockMovements.GetById;
using Application.StockMovements.GetList;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.StockMovements;

[ApiController]
[Route("inventory/movements/{movementId:guid}")]
[Tags(Tags.InventoryLedger)]
public sealed class GetStockMovementByIdController(
    IQueryHandler<GetStockMovementByIdQuery, StockMovementResponse> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType<ApiResponse<StockMovementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid movementId, CancellationToken cancellationToken)
    {
        Result<StockMovementResponse> result = await handler.Handle(
            new GetStockMovementByIdQuery(movementId),
            cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
