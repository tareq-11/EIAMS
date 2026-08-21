using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryAdjustments.UpdateLine;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

[ApiController]
[Route("inventory-adjustments/{documentId:guid}/lines")]
[Tags(Tags.WarehouseDocuments)]
public sealed class UpdateAdjustmentLineController(ICommandHandler<UpdateAdjustmentLineCommand> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] decimal Difference,
        Guid? UnitId,
        [property: JsonRequired] string Reason,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut("{lineId:guid}")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    public async Task<IResult> Handle(
        Guid documentId, Guid lineId, RequestBody request, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new UpdateAdjustmentLineCommand(
            documentId, lineId, request.Difference, request.UnitId,
            request.Reason, request.ExpectedRowVersion), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
