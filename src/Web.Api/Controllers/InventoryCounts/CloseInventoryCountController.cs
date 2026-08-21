using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryCounts.ChangeStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryCounts;

[ApiController]
[Route("inventory-counts/{countId:guid}/close")]
[Tags(Tags.InventoryCounts)]
public sealed class CloseInventoryCountController(
    ICommandHandler<ChangeInventoryCountStatusCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int ExpectedRowVersion);

    [HttpPost]
    [HasPermission(PermissionCodes.InventoryCounts.Review)]
    public async Task<IResult> Handle(Guid countId, RequestBody request, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new ChangeInventoryCountStatusCommand(
            countId, InventoryCountStatus.Closed, request.ExpectedRowVersion), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
