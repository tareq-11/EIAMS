using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.DocumentAttachments.Remove;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.DocumentAttachments;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/attachments")]
[Tags(Tags.WarehouseDocuments)]
public sealed class RemoveDocumentAttachmentController(ICommandHandler<RemoveDocumentAttachmentCommand> handler)
    : ControllerBase
{
    [HttpDelete("{attachmentId:guid}")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid documentId,
        Guid attachmentId,
        [FromQuery] int expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var command = new RemoveDocumentAttachmentCommand(documentId, attachmentId, expectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
