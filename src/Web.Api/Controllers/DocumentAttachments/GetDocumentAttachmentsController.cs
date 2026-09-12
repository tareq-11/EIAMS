using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.DocumentAttachments.GetList;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.DocumentAttachments;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/attachments")]
[Tags(Tags.WarehouseDocuments)]
public sealed class GetDocumentAttachmentsController(
    IQueryHandler<GetDocumentAttachmentsQuery, List<DocumentAttachmentResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<PagedData<DocumentAttachmentResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        Guid documentId,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        Result<List<DocumentAttachmentResponse>> result = await handler.Handle(
            new GetDocumentAttachmentsQuery(documentId, includeArchived),
            cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
