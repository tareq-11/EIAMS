using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.Post;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class PostDocumentController(
    ICommandHandler<PostDocumentCommand, PostDocumentResponse> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int ExpectedRowVersion);

    [HttpPost("{documentId:guid}/post")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Review)]
    [ProducesResponseType<ApiResponse<PostDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(Guid documentId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new PostDocumentCommand(documentId, request.ExpectedRowVersion);

        Result<PostDocumentResponse> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
