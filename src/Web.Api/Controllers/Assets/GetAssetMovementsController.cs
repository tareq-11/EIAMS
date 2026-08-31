using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Assets.GetMovements;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Assets;

[ApiController]
[Route("assets/{assetId:guid}/movements")]
[Tags(Tags.Assets)]
public sealed class GetAssetMovementsController(
    IQueryHandler<GetAssetMovementsQuery, PagedResult<AssetMovementResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Assets.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AssetMovementResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        Guid assetId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        Result<PagedResult<AssetMovementResponse>> result = await handler.Handle(
            new GetAssetMovementsQuery(assetId, pagination.Page, pagination.PageSize), cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
