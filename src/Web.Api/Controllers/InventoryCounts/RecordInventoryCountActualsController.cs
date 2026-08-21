using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryCounts.RecordActualBatch;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryCounts;

[ApiController]
[Route("inventory-counts/{countId:guid}/actuals")]
[Tags(Tags.InventoryCounts)]
public sealed class RecordInventoryCountActualsController(
    ICommandHandler<RecordInventoryCountActualsCommand> handler) : ControllerBase
{
    public sealed record ActualBody(
        [property: JsonRequired] Guid LineId,
        [property: JsonRequired] decimal ActualQuantity);

    public sealed record RequestBody(
        [property: JsonRequired] IReadOnlyCollection<ActualBody> Actuals,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut]
    [HasPermission(PermissionCodes.InventoryCounts.EnterActual)]
    public async Task<IResult> Handle(
        Guid countId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new RecordInventoryCountActualsCommand(
            countId,
            request.Actuals.Select(item => new InventoryCountActualInput(item.LineId, item.ActualQuantity)).ToArray(),
            request.ExpectedRowVersion);
        Result result = await handler.Handle(command, cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
