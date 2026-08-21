using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryAdjustments.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

[ApiController]
[Route("inventory-adjustments")]
[Tags(Tags.WarehouseDocuments)]
public sealed class CreateInventoryAdjustmentController(ICommandHandler<CreateInventoryAdjustmentCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid WarehouseId, [property: JsonRequired] string Reason);

    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseDocuments.Create)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        Result<Guid> result = await handler.Handle(
            new CreateInventoryAdjustmentCommand(request.WarehouseId, request.Reason), cancellationToken);
        return result.ToCreatedApiResponse(HttpContext, id => $"/warehouse-documents/{id}");
    }
}
