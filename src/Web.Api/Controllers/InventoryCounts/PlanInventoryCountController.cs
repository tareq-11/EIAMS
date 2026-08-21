using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryCounts.Plan;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryCounts;

[ApiController]
[Route("inventory-counts")]
[Tags(Tags.InventoryCounts)]
public sealed class PlanInventoryCountController(ICommandHandler<PlanInventoryCountCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid WarehouseId,
        [property: JsonRequired] InventoryCountType CountType,
        [property: JsonRequired] InventoryCountScopeType ScopeType,
        Guid? MaterialDomainId,
        [property: JsonRequired] IReadOnlyCollection<Guid> MaterialIds,
        [property: JsonRequired] FreezePolicy FreezePolicy);

    [HttpPost]
    [HasPermission(PermissionCodes.InventoryCounts.Plan)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new PlanInventoryCountCommand(request.WarehouseId, request.CountType,
            request.ScopeType, request.MaterialDomainId, request.MaterialIds, request.FreezePolicy);
        Result<Guid> result = await handler.Handle(command, cancellationToken);
        return result.ToCreatedApiResponse(HttpContext, id => $"/inventory-counts/{id}");
    }
}
