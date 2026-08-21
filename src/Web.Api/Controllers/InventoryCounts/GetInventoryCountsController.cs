using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.InventoryCounts.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryCounts;

[ApiController]
[Route("inventory-counts")]
[Tags(Tags.InventoryCounts)]
public sealed class GetInventoryCountsController(
    IQueryHandler<GetInventoryCountsQuery, PagedResult<InventoryCountResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.InventoryCounts.View)]
    public async Task<IResult> Handle(
        [FromQuery] Guid warehouseId,
        [FromQuery] InventoryCountStatus? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetInventoryCountsQuery(
            warehouseId, status, pagination.Page, pagination.PageSize);
        Result<PagedResult<InventoryCountResponse>> result = await handler.Handle(query, cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
