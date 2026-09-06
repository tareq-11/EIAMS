using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Reports.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Reports;

[ApiController]
[Route("reports/dashboard")]
[Tags(Tags.Reports)]
public sealed class GetDashboardReportController(
    IQueryHandler<GetDashboardReportQuery, DashboardReportResponse> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType<ApiResponse<DashboardReportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting(RateLimitingPolicies.Reporting)]
    public async Task<IResult> Handle([FromQuery] Guid? warehouseId, CancellationToken cancellationToken)
    {
        Result<DashboardReportResponse> result = await handler.Handle(
            new GetDashboardReportQuery(warehouseId), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
