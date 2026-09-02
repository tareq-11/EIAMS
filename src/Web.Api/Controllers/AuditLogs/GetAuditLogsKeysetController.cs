using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.AuditLogs;
using Application.AuditLogs.GetAuditLogs;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.AuditLogs;

public sealed record GetAuditLogsKeysetRequest(
    Guid? UserId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null,
    string? FieldName = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Search = null,
    DateTimeOffset? AfterCreatedAtUtc = null,
    Guid? AfterId = null,
    int PageSize = PaginationDefaults.DefaultPageSize);

[ApiController]
[Route("audit-logs/cursor")]
[Tags(Tags.AuditLogs)]
public sealed class GetAuditLogsKeysetController(
    IQueryHandler<GetAuditLogsKeysetQuery, KeysetPage<AuditLogListItemResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.AuditLogs.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AuditLogListItemResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] GetAuditLogsKeysetRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetAuditLogsKeysetQuery(
            request.UserId,
            request.EntityType,
            request.EntityId,
            request.Action,
            request.FieldName,
            request.FromUtc,
            request.ToUtc,
            request.Search,
            request.AfterCreatedAtUtc,
            request.AfterId,
            request.PageSize);

        Result<KeysetPage<AuditLogListItemResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToKeysetApiResponse(HttpContext);
    }
}
