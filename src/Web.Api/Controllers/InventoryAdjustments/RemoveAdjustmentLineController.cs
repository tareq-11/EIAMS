using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryAdjustments.RemoveLine;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

[ApiController]
[Route("inventory-adjustments/{documentId:guid}/lines")]
[Tags(Tags.WarehouseDocuments)]
public sealed class RemoveAdjustmentLineController(ICommandHandler<RemoveAdjustmentLineCommand> handler) : ControllerBase
{
    [HttpDelete("{lineId:guid}")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    public async Task<IResult> Handle(
        Guid documentId, Guid lineId, [FromQuery] int expectedRowVersion, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new RemoveAdjustmentLineCommand(documentId, lineId, expectedRowVersion), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
