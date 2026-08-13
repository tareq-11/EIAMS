using System.ComponentModel.DataAnnotations;
using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.DocumentAttachments.Upload;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.DocumentAttachments;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/attachments")]
[Tags(Tags.WarehouseDocuments)]
public sealed class UploadDocumentAttachmentController(ICommandHandler<UploadDocumentAttachmentCommand, Guid> handler)
    : ControllerBase
{
    public sealed class RequestForm
    {
        [Required]
        public IFormFile File { get; init; }

        [Required]
        [BindRequired]
        public AttachmentType AttachmentType { get; init; }

        [Required]
        [BindRequired]
        public int ExpectedRowVersion { get; init; }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IResult> Handle(Guid documentId, [FromForm] RequestForm request, CancellationToken cancellationToken)
    {
        await using Stream content = request.File.OpenReadStream();

        var command = new UploadDocumentAttachmentCommand(
            documentId,
            request.AttachmentType,
            content,
            request.File.FileName,
            request.File.ContentType,
            request.File.Length,
            request.ExpectedRowVersion);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToCreatedApiResponse(
            HttpContext,
            attachmentId => $"/warehouse-documents/{documentId}/attachments/{attachmentId}/content");
    }
}
