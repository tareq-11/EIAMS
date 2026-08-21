using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.InventoryCounts.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryCounts;

[ApiController]
[Route("inventory-counts/{countId:guid}")]
[Tags(Tags.InventoryCounts)]
public sealed class GetInventoryCountController(IQueryHandler<GetInventoryCountByIdQuery, InventoryCountDetailsResponse> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.InventoryCounts.View)]
    public async Task<IResult> Handle(Guid countId, CancellationToken cancellationToken)
    {
        Result<InventoryCountDetailsResponse> result = await handler.Handle(new GetInventoryCountByIdQuery(countId), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
