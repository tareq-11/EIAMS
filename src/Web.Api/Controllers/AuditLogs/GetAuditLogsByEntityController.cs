using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.AuditLogs;
using Application.AuditLogs.GetByEntity;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.AuditLogs;

[ApiController]
[Route("audit-logs/entities")]
[Tags(Tags.AuditLogs)]
public sealed class GetAuditLogsByEntityController(
    IQueryHandler<GetAuditLogsByEntityQuery, PagedResult<AuditLogListItemResponse>> handler) : ControllerBase
{
    [HttpGet("{entityType}/{entityId:guid}")]
    [HasPermission(PermissionCodes.AuditLogs.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AuditLogListItemResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        string entityType,
        Guid entityId,
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetAuditLogsByEntityQuery(
            entityType, entityId, action, fromUtc, toUtc, pagination.Page, pagination.PageSize);
        Result<PagedResult<AuditLogListItemResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
