using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.DocumentLineAssetSelections.Add;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.DocumentLineAssetSelections;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/lines/{lineId:guid}/assets")]
[Tags(Tags.WarehouseDocuments)]
public sealed class AddDocumentLineAssetSelectionController(
    ICommandHandler<AddDocumentLineAssetSelectionCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid AssetId,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid documentId,
        Guid lineId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new AddDocumentLineAssetSelectionCommand(
            documentId,
            lineId,
            request.AssetId,
            request.ExpectedRowVersion);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToCreatedApiResponse(HttpContext, id => $"/warehouse-documents/{documentId}/lines/{lineId}/assets/{id}");
    }
}
