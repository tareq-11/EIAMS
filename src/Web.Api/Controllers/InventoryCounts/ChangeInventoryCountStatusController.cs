using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryCounts.ChangeStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryCounts;

[ApiController]
[Route("inventory-counts/{countId:guid}/status")]
[Tags(Tags.InventoryCounts)]
public sealed class ChangeInventoryCountStatusController(ICommandHandler<ChangeInventoryCountStatusCommand> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] InventoryCountStatus TargetStatus,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut]
    [HasPermission(PermissionCodes.InventoryCounts.Review)]
    public async Task<IResult> Handle(Guid countId, RequestBody request, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new ChangeInventoryCountStatusCommand(
            countId, request.TargetStatus, request.ExpectedRowVersion), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
