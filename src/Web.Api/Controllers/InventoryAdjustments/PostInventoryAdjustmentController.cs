using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.Post;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

[ApiController]
[Route("adjustments")]
[Tags(Tags.WarehouseDocuments)]
public sealed class PostInventoryAdjustmentController(
    ICommandHandler<PostDocumentCommand, PostDocumentResponse> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int ExpectedRowVersion);

    [HttpPost("{id:guid}/post")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Review)]
    [ProducesResponseType<ApiResponse<PostDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid id,
        RequestBody request,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new PostDocumentCommand(
            id,
            request.ExpectedRowVersion,
            idempotencyKey,
            DocumentType.Adjustment);

        Result<PostDocumentResponse> result = await handler.Handle(command, cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
