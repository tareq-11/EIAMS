using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.DocumentLineAssetSelections.Remove;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.DocumentLineAssetSelections;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/lines/{lineId:guid}/assets/{assetId:guid}")]
[Tags(Tags.WarehouseDocuments)]
public sealed class RemoveDocumentLineAssetSelectionController(
    ICommandHandler<RemoveDocumentLineAssetSelectionCommand> handler) : ControllerBase
{
    [HttpDelete]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid documentId,
        Guid lineId,
        Guid assetId,
        [FromQuery] int expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var command = new RemoveDocumentLineAssetSelectionCommand(
            documentId,
            lineId,
            assetId,
            expectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
