using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class GetWarehouseDocumentByIdController(
    IQueryHandler<GetWarehouseDocumentByIdQuery, WarehouseDocumentDetailsResponse> handler)
    : ControllerBase
{
    [HttpGet("{documentId:guid}")]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<WarehouseDocumentDetailsResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid documentId, CancellationToken cancellationToken)
    {
        var query = new GetWarehouseDocumentByIdQuery(documentId);

        Result<WarehouseDocumentDetailsResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
