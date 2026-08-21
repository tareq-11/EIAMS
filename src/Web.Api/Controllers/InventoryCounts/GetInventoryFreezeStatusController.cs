using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryCounts.GetFreezeStatus;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryCounts;

[ApiController]
[Route("warehouses/{warehouseId:guid}/inventory-freeze-status")]
[Tags(Tags.InventoryCounts)]
public sealed class GetInventoryFreezeStatusController(
    IQueryHandler<GetInventoryFreezeStatusQuery, InventoryFreezeStatusResponse> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.InventoryCounts.View)]
    [ProducesResponseType<ApiResponse<InventoryFreezeStatusResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid warehouseId, CancellationToken cancellationToken)
    {
        Result<InventoryFreezeStatusResponse> result = await handler.Handle(
            new GetInventoryFreezeStatusQuery(warehouseId),
            cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
