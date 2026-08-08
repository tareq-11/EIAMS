using Application.Abstractions.Messaging;
using Application.WarehouseMaterialSettings.GetByWarehouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseMaterialSettings;

[ApiController]
[Route("warehouses")]
[Tags(Tags.WarehouseMaterialSettings)]
public sealed class GetWarehouseMaterialSettingsController(
    IQueryHandler<GetWarehouseMaterialSettingsQuery, List<WarehouseMaterialSettingResponse>> handler)
    : ControllerBase
{
    [HttpGet("{warehouseId:guid}/material-settings")]
    [Authorize]
    [ProducesResponseType<ApiResponse<List<WarehouseMaterialSettingResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid warehouseId, CancellationToken cancellationToken)
    {
        var query = new GetWarehouseMaterialSettingsQuery(warehouseId);

        Result<List<WarehouseMaterialSettingResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
