using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.InventoryCounts.GetLines;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryCounts;

[ApiController]
[Route("inventory-counts/{countId:guid}/lines")]
[Tags(Tags.InventoryCounts)]
public sealed class GetInventoryCountLinesController(
    IQueryHandler<GetInventoryCountLinesQuery, PagedResult<InventoryCountLineResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.InventoryCounts.View)]
    public async Task<IResult> Handle(
        Guid countId,
        [FromQuery] bool onlyVariance,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetInventoryCountLinesQuery(
            countId, onlyVariance, pagination.Page, pagination.PageSize);
        Result<PagedResult<InventoryCountLineResponse>> result = await handler.Handle(query, cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
