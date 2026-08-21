using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryAdjustments.CreateDisposal;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

[ApiController]
[Route("inventory-adjustments/disposals")]
[Tags(Tags.Assets)]
public sealed class CreateDisposalController(ICommandHandler<CreateDisposalCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid WarehouseId,
        [property: JsonRequired] IReadOnlyCollection<Guid> AssetIds,
        [property: JsonRequired] string Reason);

    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseDocuments.Create)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        Result<Guid> result = await handler.Handle(
            new CreateDisposalCommand(request.WarehouseId, request.AssetIds, request.Reason), cancellationToken);
        return result.ToCreatedApiResponse(HttpContext, id => $"/warehouse-documents/{id}");
    }
}
