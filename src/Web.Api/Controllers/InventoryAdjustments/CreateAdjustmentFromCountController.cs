using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryAdjustments.CreateFromCount;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

[ApiController]
[Route("inventory-counts/{countId:guid}/adjustment")]
[Tags(Tags.InventoryCounts)]
public sealed class CreateAdjustmentFromCountController(ICommandHandler<CreateAdjustmentFromCountCommand, Guid> handler) : ControllerBase
{
    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseDocuments.Create)]
    public async Task<IResult> Handle(Guid countId, CancellationToken cancellationToken)
    {
        Result<Guid> result = await handler.Handle(new CreateAdjustmentFromCountCommand(countId), cancellationToken);
        return result.ToCreatedApiResponse(HttpContext, id => $"/warehouse-documents/{id}");
    }
}
