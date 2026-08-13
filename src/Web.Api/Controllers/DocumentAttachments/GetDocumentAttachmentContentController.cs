using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.DocumentAttachments.GetContent;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.DocumentAttachments;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/attachments")]
[Tags(Tags.WarehouseDocuments)]
public sealed class GetDocumentAttachmentContentController(
    IQueryHandler<GetDocumentAttachmentContentQuery, DocumentAttachmentContentResponse> handler)
    : ControllerBase
{
    // File downloads bypass the standard ApiResponse<T> JSON envelope by design - the response body
    // is the raw file content, with Content-Type/Content-Disposition set from the stored metadata.
    [HttpGet("{attachmentId:guid}/content")]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid documentId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var query = new GetDocumentAttachmentContentQuery(documentId, attachmentId);

        Result<DocumentAttachmentContentResponse> result = await handler.Handle(query, cancellationToken);

        if (result.IsFailure)
        {
            return CustomResults.Problem(result, HttpContext);
        }

        DocumentAttachmentContentResponse content = result.Value;

        return Results.File(content.Content, content.MimeType, content.OriginalFilename);
    }
}
