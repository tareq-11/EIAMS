using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.CompleteDraft;
using Application.WarehouseDocuments.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class CompleteWarehouseDocumentDraftController(
    ICommandHandler<CompleteWarehouseDocumentDraftCommand, WarehouseDocumentDetailsResponse> handler)
    : ControllerBase
{
    /// <summary>
    /// Creates a complete draft aggregate atomically. Binary attachments are added afterward through
    /// the existing attachment endpoints and are not part of this request.
    /// </summary>
    [HttpPost("complete-draft")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Create)]
    [ProducesResponseType<ApiResponse<WarehouseDocumentDetailsResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromBody] CompleteWarehouseDocumentDraftCommand request,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        Result<WarehouseDocumentDetailsResponse> result = await handler.Handle(request with { IdempotencyKey = idempotencyKey }, cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
