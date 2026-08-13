using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.UpdatePaperReference;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class UpdateDocumentPaperReferenceController(ICommandHandler<UpdateDocumentPaperReferenceCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(
        string? PaperDocumentNumber,
        int? PaperDocumentYear,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut("{documentId:guid}/paper-reference")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(Guid documentId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateDocumentPaperReferenceCommand(
            documentId,
            request.PaperDocumentNumber,
            request.PaperDocumentYear,
            request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
