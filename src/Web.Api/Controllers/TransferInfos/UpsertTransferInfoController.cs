using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.TransferInfos.Upsert;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.TransferInfos;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/transfer-info")]
[Tags(Tags.WarehouseDocuments)]
public sealed class UpsertTransferInfoController(ICommandHandler<UpsertTransferInfoCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid DestinationWarehouseId,
        [property: JsonRequired] string TransferReason,
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
        var command = new UpsertTransferInfoCommand(
            documentId,
            request.DestinationWarehouseId,
            request.TransferReason,
            request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
