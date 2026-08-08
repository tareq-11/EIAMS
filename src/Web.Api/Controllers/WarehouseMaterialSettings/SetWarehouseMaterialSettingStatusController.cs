using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseMaterialSettings.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseMaterialSettings;

[ApiController]
[Route("warehouse-material-settings")]
[Tags(Tags.WarehouseMaterialSettings)]
public sealed class SetWarehouseMaterialSettingStatusController(
    ICommandHandler<SetWarehouseMaterialSettingStatusCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{settingId:guid}/status")]
    [HasPermission(PermissionCodes.WarehouseMaterialSettings.Manage)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid settingId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetWarehouseMaterialSettingStatusCommand(settingId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
