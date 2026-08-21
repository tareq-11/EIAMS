using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.ReturnInfos.Upsert;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.ReturnInfos;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/return-info")]
[Tags(Tags.WarehouseDocuments)]
public sealed class UpsertReturnInfoController(ICommandHandler<UpsertReturnInfoCommand> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid OriginalIssueDocumentId,
        [property: JsonRequired] string ReturnReason,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid documentId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new UpsertReturnInfoCommand(
            documentId,
            request.OriginalIssueDocumentId,
            request.ReturnReason,
            request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
