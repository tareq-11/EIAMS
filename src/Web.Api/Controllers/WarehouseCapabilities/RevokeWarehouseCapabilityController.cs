using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseCapabilities.Revoke;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseCapabilities;

[ApiController]
[Route("warehouse-capabilities")]
[Tags(Tags.WarehouseCapabilities)]
public sealed class RevokeWarehouseCapabilityController(ICommandHandler<RevokeWarehouseCapabilityCommand> handler)
    : ControllerBase
{
    [HttpDelete("{capabilityId:guid}")]
    [HasPermission(PermissionCodes.WarehouseCapabilities.Manage)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(Guid capabilityId, CancellationToken cancellationToken)
    {
        var command = new RevokeWarehouseCapabilityCommand(capabilityId);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
