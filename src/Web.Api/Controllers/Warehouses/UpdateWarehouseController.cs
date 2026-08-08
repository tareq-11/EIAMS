using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Warehouses.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Warehouses;

[ApiController]
[Route("warehouses")]
[Tags(Tags.Warehouses)]
public sealed class UpdateWarehouseController(ICommandHandler<UpdateWarehouseCommand> handler) : ControllerBase
{
    public sealed record RequestBody(string Name, string WarehouseType, [property: JsonRequired] bool CanHoldStock, [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut("{warehouseId:guid}")]
    [HasPermission(PermissionCodes.Warehouses.Manage)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(Guid warehouseId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateWarehouseCommand(
            warehouseId,
            request.Name,
            request.WarehouseType,
            request.CanHoldStock,
            request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
