using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryAdjustments.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

[ApiController]
[Route("inventory-adjustments/{adjustmentId:guid}")]
[Tags(Tags.InventoryCounts)]
public sealed class GetInventoryAdjustmentByIdController(
    IQueryHandler<GetInventoryAdjustmentByIdQuery, InventoryAdjustmentDetailsResponse> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<InventoryAdjustmentDetailsResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid adjustmentId, CancellationToken cancellationToken)
    {
        Result<InventoryAdjustmentDetailsResponse> result = await handler.Handle(
            new GetInventoryAdjustmentByIdQuery(adjustmentId), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
