using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryCounts.SetVarianceReason;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryCounts;

[ApiController]
[Route("inventory-counts/{countId:guid}/lines/{lineId:guid}/variance-reason")]
[Tags(Tags.InventoryCounts)]
public sealed class SetInventoryCountVarianceReasonController(ICommandHandler<SetInventoryCountVarianceReasonCommand> handler) : ControllerBase
{
    public sealed record RequestBody(string? Reason, [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut]
    [HasPermission(PermissionCodes.InventoryCounts.Review)]
    public async Task<IResult> Handle(Guid countId, Guid lineId, RequestBody request, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetInventoryCountVarianceReasonCommand(
            countId, lineId, request.Reason, request.ExpectedRowVersion), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
