using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseCapabilityOperations.AddOperation;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseCapabilityOperations;

[ApiController]
[Route("warehouse-capabilities/{capabilityId:guid}/operations")]
[Tags(Tags.WarehouseCapabilityOperations)]
public sealed class AddWarehouseCapabilityOperationController(
    ICommandHandler<AddWarehouseCapabilityOperationCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int OperationType);

    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseCapabilities.Manage)]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(Guid capabilityId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new AddWarehouseCapabilityOperationCommand(capabilityId, (OperationType)request.OperationType);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToCreatedApiResponse(
            HttpContext,
            _ => $"/warehouse-capabilities/{capabilityId}/operations");
    }
}
