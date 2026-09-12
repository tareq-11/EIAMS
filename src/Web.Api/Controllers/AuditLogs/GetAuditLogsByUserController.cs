using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.AuditLogs;
using Application.AuditLogs.GetByUser;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.AuditLogs;

[ApiController]
[Route("audit-logs/users")]
[Tags(Tags.AuditLogs)]
public sealed class GetAuditLogsByUserController(
    IQueryHandler<GetAuditLogsByUserQuery, PagedResult<AuditLogListItemResponse>> handler) : ControllerBase
{
    [HttpGet("{userId:guid}")]
    [HasPermission(PermissionCodes.AuditLogs.View)]
    [ProducesResponseType<ApiResponse<PagedData<AuditLogListItemResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        Guid userId,
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetAuditLogsByUserQuery(
            userId, entityType, action, fromUtc, toUtc, pagination.Page, pagination.PageSize);
        Result<PagedResult<AuditLogListItemResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
