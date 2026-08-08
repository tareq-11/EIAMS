using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseCapabilityOperations.RemoveOperation;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseCapabilityOperations;

[ApiController]
[Route("warehouse-capabilities/{capabilityId:guid}/operations")]
[Tags(Tags.WarehouseCapabilityOperations)]
public sealed class RemoveWarehouseCapabilityOperationController(
    ICommandHandler<RemoveWarehouseCapabilityOperationCommand> handler) : ControllerBase
{
    [HttpDelete("{operationType}")]
    [HasPermission(PermissionCodes.WarehouseCapabilities.Manage)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        Guid capabilityId,
        OperationType operationType,
        CancellationToken cancellationToken)
    {
        var command = new RemoveWarehouseCapabilityOperationCommand(capabilityId, operationType);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
