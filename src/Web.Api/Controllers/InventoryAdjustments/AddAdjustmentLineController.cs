using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryAdjustments.AddLine;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

[ApiController]
[Route("inventory-adjustments/{documentId:guid}/lines")]
[Tags(Tags.WarehouseDocuments)]
public sealed class AddAdjustmentLineController(ICommandHandler<AddAdjustmentLineCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid MaterialId,
        [property: JsonRequired] decimal Difference,
        Guid? UnitId,
        [property: JsonRequired] string Reason,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    public async Task<IResult> Handle(Guid documentId, RequestBody request, CancellationToken cancellationToken)
    {
        Result<Guid> result = await handler.Handle(new AddAdjustmentLineCommand(
            documentId, request.MaterialId, request.Difference, request.UnitId,
            request.Reason, request.ExpectedRowVersion), cancellationToken);
        return result.ToCreatedApiResponse(HttpContext, _ => $"/warehouse-documents/{documentId}");
    }
}
