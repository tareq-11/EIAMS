using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.DocumentLines.Update;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.DocumentLines;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/lines")]
[Tags(Tags.WarehouseDocuments)]
public sealed class UpdateDocumentLineController(ICommandHandler<UpdateDocumentLineCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] decimal Quantity,
        Guid? UnitId,
        decimal? UnitPrice,
        string? BatchNumber,
        DateOnly? ExpiryDate,
        OpeningType? OpeningType,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut("{lineId:guid}")]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid documentId,
        Guid lineId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDocumentLineCommand(
            documentId,
            lineId,
            request.Quantity,
            request.UnitId,
            request.UnitPrice,
            request.BatchNumber,
            request.ExpiryDate,
            request.OpeningType,
            request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
