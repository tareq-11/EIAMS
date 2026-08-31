using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Returns.GetEligibleItems;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Returns;

[ApiController]
[Route("returns/eligible-items")]
[Tags(Tags.WarehouseDocuments)]
public sealed class GetReturnEligibleItemsController(
    IQueryHandler<GetReturnEligibleItemsQuery, IReadOnlyList<ReturnEligibleItemResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ReturnEligibleItemResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        [FromQuery] Guid originalIssueDocumentId,
        CancellationToken cancellationToken)
    {
        var query = new GetReturnEligibleItemsQuery(originalIssueDocumentId);
        Result<IReadOnlyList<ReturnEligibleItemResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
