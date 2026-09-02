using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.CreateReversal;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class CreateReversalDocumentController(ICommandHandler<CreateReversalDocumentCommand, Guid> handler)
    : ControllerBase
{
    [HttpPost("{documentId:guid}/reversals")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Create)]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid documentId,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new CreateReversalDocumentCommand(documentId, idempotencyKey);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToCreatedApiResponse(
            HttpContext,
            reversalId => $"/api/v1/warehouse-documents/{reversalId}");
    }
}
