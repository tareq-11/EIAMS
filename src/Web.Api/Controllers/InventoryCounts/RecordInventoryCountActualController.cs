using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryCounts.RecordActual;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryCounts;

[ApiController]
[Route("inventory-counts/{countId:guid}/lines/{lineId:guid}/actual")]
[Tags(Tags.InventoryCounts)]
public sealed class RecordInventoryCountActualController(ICommandHandler<RecordInventoryCountActualCommand> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] decimal ActualQuantity,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut]
    [HasPermission(PermissionCodes.InventoryCounts.EnterActual)]
    public async Task<IResult> Handle(Guid countId, Guid lineId, RequestBody request, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new RecordInventoryCountActualCommand(
            countId, lineId, request.ActualQuantity, request.ExpectedRowVersion), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
