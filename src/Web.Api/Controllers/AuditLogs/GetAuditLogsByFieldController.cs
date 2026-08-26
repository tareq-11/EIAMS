using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.AuditLogs;
using Application.AuditLogs.GetByField;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.AuditLogs;

[ApiController]
[Route("audit-logs/fields")]
[Tags(Tags.AuditLogs)]
public sealed class GetAuditLogsByFieldController(
    IQueryHandler<GetAuditLogsByFieldQuery, PagedResult<AuditLogListItemResponse>> handler) : ControllerBase
{
    [HttpGet("{fieldName}")]
    [HasPermission(PermissionCodes.AuditLogs.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AuditLogListItemResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        string fieldName,
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid? userId,
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetAuditLogsByFieldQuery(
            fieldName,
            entityType,
            entityId,
            userId,
            action,
            fromUtc,
            toUtc,
            pagination.Page,
            pagination.PageSize);
        Result<PagedResult<AuditLogListItemResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
