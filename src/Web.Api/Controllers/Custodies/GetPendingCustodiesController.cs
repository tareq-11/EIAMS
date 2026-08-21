using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Custodies.GetPending;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Custodies;

[ApiController]
[Route("custodies/pending")]
[Tags(Tags.Assets)]
public sealed class GetPendingCustodiesController(
    IQueryHandler<GetPendingCustodiesQuery, PagedResult<PendingCustodyResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PendingCustodyResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        [FromQuery] Guid warehouseId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetPendingCustodiesQuery(warehouseId, pagination.Page, pagination.PageSize);
        Result<PagedResult<PendingCustodyResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
