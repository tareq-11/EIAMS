using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.DocumentLines.Add;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.DocumentLines;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/lines")]
[Tags(Tags.WarehouseDocuments)]
public sealed class AddDocumentLineController(ICommandHandler<AddDocumentLineCommand, Guid> handler)
    : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid MaterialId,
        [property: JsonRequired] decimal Quantity,
        Guid? UnitId,
        decimal? UnitPrice,
        string? BatchNumber,
        DateOnly? ExpiryDate,
        OpeningType? OpeningType,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(Guid documentId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new AddDocumentLineCommand(
            documentId,
            request.MaterialId,
            request.Quantity,
            request.UnitId,
            request.UnitPrice,
            request.BatchNumber,
            request.ExpiryDate,
            request.OpeningType,
            request.ExpectedRowVersion);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        // Document details is the canonical readable representation and includes all of its lines.
        return result.ToCreatedApiResponse(HttpContext, _ => $"/warehouse-documents/{documentId}");
    }
}
