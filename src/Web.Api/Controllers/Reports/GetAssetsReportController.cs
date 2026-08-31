using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Reports.Assets;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Reports;

[ApiController]
[Route("reports/assets")]
[Tags(Tags.Reports)]
public sealed class GetAssetsReportController(
    IQueryHandler<GetAssetsReportQuery, PagedResult<AssetsReportRow>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Assets.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AssetsReportRow>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] Guid? warehouseId,
        [FromQuery] AssetCurrentStatus? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        Result<PagedResult<AssetsReportRow>> result = await handler.Handle(
            new GetAssetsReportQuery(warehouseId, status, pagination.Page, pagination.PageSize), cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
