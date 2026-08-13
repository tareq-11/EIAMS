using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.ReturnToDraft;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class ReturnDocumentToDraftController(ICommandHandler<ReturnDocumentToDraftCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int ExpectedRowVersion);

    [HttpPost("{documentId:guid}/return-to-draft")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(Guid documentId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new ReturnDocumentToDraftCommand(documentId, request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
