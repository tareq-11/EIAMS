using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.Submit;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class SubmitDocumentController(ICommandHandler<SubmitDocumentCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int ExpectedRowVersion);

    [HttpPost("{documentId:guid}/submit")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Submit)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(Guid documentId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SubmitDocumentCommand(documentId, request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
