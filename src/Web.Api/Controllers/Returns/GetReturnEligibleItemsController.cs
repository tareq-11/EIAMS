using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Returns.GetEligibleItems;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Returns;

[ApiController]
[Route("returns/eligible-items")]
[Tags(Tags.WarehouseDocuments)]
public sealed class GetReturnEligibleItemsController(
    IQueryHandler<GetReturnEligibleItemsQuery, PagedResult<ReturnEligibleItemResponse>> handler)
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
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetReturnEligibleItemsQuery(originalIssueDocumentId, pagination.Page, pagination.PageSize);
        Result<PagedResult<ReturnEligibleItemResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
