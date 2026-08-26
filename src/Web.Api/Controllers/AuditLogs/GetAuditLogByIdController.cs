using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.AuditLogs;
using Application.AuditLogs.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.AuditLogs;

[ApiController]
[Route("audit-logs")]
[Tags(Tags.AuditLogs)]
public sealed class GetAuditLogByIdController(
    IQueryHandler<GetAuditLogByIdQuery, AuditLogDetailsResponse> handler) : ControllerBase
{
    [HttpGet("{auditLogId:guid}")]
    [HasPermission(PermissionCodes.AuditLogs.View)]
    [ProducesResponseType<ApiResponse<AuditLogDetailsResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid auditLogId, CancellationToken cancellationToken)
    {
        var query = new GetAuditLogByIdQuery(auditLogId);
        Result<AuditLogDetailsResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
