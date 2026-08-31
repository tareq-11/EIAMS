using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.AuditLogs;
using Application.AuditLogs.GetAuditLogs;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.AuditLogs;

public sealed record GetAuditLogsRequest(
    Guid? UserId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null,
    string? FieldName = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Search = null);

[ApiController]
[Route("audit-logs")]
[Tags(Tags.AuditLogs)]
public sealed class GetAuditLogsController(
    IQueryHandler<GetAuditLogsQuery, PagedResult<AuditLogListItemResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.AuditLogs.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AuditLogListItemResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] GetAuditLogsRequest request,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetAuditLogsQuery(
            request.UserId,
            request.EntityType,
            request.EntityId,
            request.Action,
            request.FieldName,
            request.FromUtc,
            request.ToUtc,
            request.Search,
            pagination.Page,
            pagination.PageSize);

        Result<PagedResult<AuditLogListItemResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
