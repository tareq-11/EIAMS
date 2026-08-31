using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.GetPolicy;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class GetWarehouseDocumentPolicyController(
    IQueryHandler<GetWarehouseDocumentPolicyQuery, WarehouseDocumentPolicyResponse> handler) : ControllerBase
{
    [HttpGet("{documentId:guid}/policy")]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<WarehouseDocumentPolicyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid documentId, CancellationToken cancellationToken)
    {
        Result<WarehouseDocumentPolicyResponse> result = await handler.Handle(
            new GetWarehouseDocumentPolicyQuery(documentId),
            cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
