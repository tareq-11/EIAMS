using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Reports.CountAdjustments;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Reports;

[ApiController]
[Route("reports/count-adjustments")]
[Tags(Tags.Reports)]
public sealed class GetCountAdjustmentsReportController(
    IQueryHandler<GetCountAdjustmentsReportQuery, PagedResult<CountAdjustmentsReportRow>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.InventoryCounts.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CountAdjustmentsReportRow>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] Guid? warehouseId,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        Result<PagedResult<CountAdjustmentsReportRow>> result = await handler.Handle(
            new GetCountAdjustmentsReportQuery(
                warehouseId, fromUtc, toUtc, pagination.Page, pagination.PageSize), cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
