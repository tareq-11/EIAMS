using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Warehouses.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Warehouses;

[ApiController]
[Route("warehouses")]
[Tags(Tags.Warehouses)]
public sealed class CreateWarehouseController(ICommandHandler<CreateWarehouseCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid SiteId, string Name, string Code, string WarehouseType, [property: JsonRequired] bool CanHoldStock);

    [HttpPost]
    [HasPermission(PermissionCodes.Warehouses.Manage)]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateWarehouseCommand(
            request.SiteId,
            request.Name,
            request.Code,
            request.WarehouseType,
            request.CanHoldStock);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToCreatedApiResponse(HttpContext, warehouseId => $"/warehouses/{warehouseId}");
    }
}
