using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.CreateReversal;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

[ApiController]
[Route("adjustments")]
[Tags(Tags.WarehouseDocuments)]
public sealed class ReverseInventoryAdjustmentController(
    ICommandHandler<CreateReversalDocumentCommand, Guid> handler) : ControllerBase
{
    [HttpPost("{id:guid}/reverse")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Create)]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new CreateReversalDocumentCommand(
            id,
            idempotencyKey,
            DocumentType.Adjustment);

        Result<Guid> result = await handler.Handle(command, cancellationToken);
        return result.ToCreatedApiResponse(
            HttpContext,
            reversalId => $"/api/v1/warehouse-documents/{reversalId}");
    }
}
