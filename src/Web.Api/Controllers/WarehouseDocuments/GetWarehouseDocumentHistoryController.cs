using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.GetHistory;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class GetWarehouseDocumentHistoryController(
    IQueryHandler<GetWarehouseDocumentHistoryQuery, WarehouseDocumentHistoryResponse> handler) : ControllerBase
{
    [HttpGet("{documentId:guid}/history")]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<WarehouseDocumentHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid documentId, CancellationToken cancellationToken)
    {
        Result<WarehouseDocumentHistoryResponse> result = await handler.Handle(
            new GetWarehouseDocumentHistoryQuery(documentId),
            cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
