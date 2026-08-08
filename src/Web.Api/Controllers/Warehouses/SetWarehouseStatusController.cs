using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Warehouses.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Warehouses;

[ApiController]
[Route("warehouses")]
[Tags(Tags.Warehouses)]
public sealed class SetWarehouseStatusController(ICommandHandler<SetWarehouseStatusCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status, [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut("{warehouseId:guid}/status")]
    [HasPermission(PermissionCodes.Warehouses.Manage)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(Guid warehouseId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetWarehouseStatusCommand(
            warehouseId,
            (Status)request.Status,
            request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
