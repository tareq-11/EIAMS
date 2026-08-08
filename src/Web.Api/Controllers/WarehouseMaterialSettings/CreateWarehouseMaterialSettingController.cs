using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseMaterialSettings.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseMaterialSettings;

[ApiController]
[Route("warehouse-material-settings")]
[Tags(Tags.WarehouseMaterialSettings)]
public sealed class CreateWarehouseMaterialSettingController(
    ICommandHandler<CreateWarehouseMaterialSettingCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid WarehouseId, [property: JsonRequired] Guid MaterialId, [property: JsonRequired] decimal MinQuantity, [property: JsonRequired] decimal MaxQuantity);

    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseMaterialSettings.Manage)]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateWarehouseMaterialSettingCommand(
            request.WarehouseId,
            request.MaterialId,
            request.MinQuantity,
            request.MaxQuantity);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToCreatedApiResponse(
            HttpContext,
            _ => $"/warehouses/{request.WarehouseId}/material-settings");
    }
}
