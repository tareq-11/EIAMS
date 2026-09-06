using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Reports.Documents;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Reports;

[ApiController]
[Route("reports/documents")]
[Tags(Tags.Reports)]
public sealed class GetDocumentsReportController(
    IQueryHandler<GetDocumentsReportQuery, PagedResult<DocumentsReportRow>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DocumentsReportRow>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting(RateLimitingPolicies.Reporting)]
    public async Task<IResult> Handle(
        [FromQuery] Guid? warehouseId,
        [FromQuery] DocumentType? documentType,
        [FromQuery] DocumentStatus? status,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        Result<PagedResult<DocumentsReportRow>> result = await handler.Handle(
            new GetDocumentsReportQuery(
                warehouseId, documentType, status, fromUtc, toUtc, pagination.Page, pagination.PageSize),
            cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
