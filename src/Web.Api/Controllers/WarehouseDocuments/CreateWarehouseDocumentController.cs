using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.Create;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class CreateWarehouseDocumentController(ICommandHandler<CreateWarehouseDocumentCommand, Guid> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid WarehouseId, [property: JsonRequired] int DocumentType);

    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseDocuments.Create)]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateWarehouseDocumentCommand(request.WarehouseId, (DocumentType)request.DocumentType);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToCreatedApiResponse(HttpContext, documentId => $"/warehouse-documents/{documentId}");
    }
}
