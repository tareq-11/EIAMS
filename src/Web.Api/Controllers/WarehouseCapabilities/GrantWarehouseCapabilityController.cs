using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseCapabilities.Grant;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseCapabilities;

[ApiController]
[Route("warehouse-capabilities")]
[Tags(Tags.WarehouseCapabilities)]
public sealed class GrantWarehouseCapabilityController(ICommandHandler<GrantWarehouseCapabilityCommand, Guid> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid WarehouseId, [property: JsonRequired] Guid MaterialDomainId);

    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseCapabilities.Manage)]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new GrantWarehouseCapabilityCommand(request.WarehouseId, request.MaterialDomainId);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToCreatedApiResponse(
            HttpContext,
            _ => $"/warehouses/{request.WarehouseId}/capabilities");
    }
}
