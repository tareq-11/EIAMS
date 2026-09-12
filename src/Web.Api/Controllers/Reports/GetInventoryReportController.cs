using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Reports.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Reports;

[ApiController]
[Route("reports/inventory")]
[Tags(Tags.Reports)]
public sealed class GetInventoryReportController(
    IQueryHandler<GetInventoryReportQuery, PagedResult<InventoryReportRow>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType<ApiResponse<PagedData<InventoryReportRow>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting(RateLimitingPolicies.Reporting)]
    public async Task<IResult> Handle(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? materialId,
        [FromQuery] string? search,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        Result<PagedResult<InventoryReportRow>> result = await handler.Handle(
            new GetInventoryReportQuery(warehouseId, materialId, search, pagination.Page, pagination.PageSize),
            cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
