using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseMaterialSettings.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseMaterialSettings;

[ApiController]
[Route("warehouse-material-settings")]
[Tags(Tags.WarehouseMaterialSettings)]
public sealed class UpdateWarehouseMaterialSettingController(
    ICommandHandler<UpdateWarehouseMaterialSettingCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] decimal MinQuantity, [property: JsonRequired] decimal MaxQuantity);

    [HttpPut("{settingId:guid}")]
    [HasPermission(PermissionCodes.WarehouseMaterialSettings.Manage)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid settingId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateWarehouseMaterialSettingCommand(
            settingId,
            request.MinQuantity,
            request.MaxQuantity);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
