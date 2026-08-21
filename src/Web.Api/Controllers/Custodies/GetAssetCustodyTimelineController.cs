using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Custodies.GetTimeline;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Custodies;

[ApiController]
[Route("assets/{assetId:guid}/custody-timeline")]
[Tags(Tags.Assets)]
public sealed class GetAssetCustodyTimelineController(
    IQueryHandler<GetAssetCustodyTimelineQuery, PagedResult<AssetCustodyTimelineResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AssetCustodyTimelineResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        Guid assetId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetAssetCustodyTimelineQuery(assetId, pagination.Page, pagination.PageSize);
        Result<PagedResult<AssetCustodyTimelineResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
