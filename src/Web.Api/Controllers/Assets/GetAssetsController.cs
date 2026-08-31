using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Assets.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Assets;

public sealed record GetAssetsRequest(
    Guid? WarehouseId,
    Guid? MaterialId,
    AssetCurrentStatus? Status,
    string? Search);

[ApiController]
[Route("assets")]
[Tags(Tags.Assets)]
public sealed class GetAssetsController(IQueryHandler<GetAssetsQuery, PagedResult<AssetResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Assets.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AssetResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] GetAssetsRequest request,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetAssetsQuery(
            request.WarehouseId,
            request.MaterialId,
            request.Status,
            request.Search,
            pagination.Page,
            pagination.PageSize);
        Result<PagedResult<AssetResponse>> result = await handler.Handle(query, cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
