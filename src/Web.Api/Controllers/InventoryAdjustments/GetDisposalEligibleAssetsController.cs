using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.InventoryAdjustments.GetDisposalEligibleAssets;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

public sealed record GetDisposalEligibleAssetsRequest(Guid? WarehouseId, Guid? MaterialId, string? Search);

[ApiController]
[Route("inventory-adjustments/disposal-eligible-assets")]
[Tags(Tags.Assets)]
public sealed class GetDisposalEligibleAssetsController(
    IQueryHandler<GetDisposalEligibleAssetsQuery, PagedResult<DisposalEligibleAssetResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Assets.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DisposalEligibleAssetResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] GetDisposalEligibleAssetsRequest request,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetDisposalEligibleAssetsQuery(
            request.WarehouseId, request.MaterialId, request.Search, pagination.Page, pagination.PageSize);
        Result<PagedResult<DisposalEligibleAssetResponse>> result = await handler.Handle(query, cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
