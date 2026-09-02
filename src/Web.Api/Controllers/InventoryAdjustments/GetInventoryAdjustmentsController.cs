using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.InventoryAdjustments.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.InventoryAdjustments;

public sealed record GetInventoryAdjustmentsRequest(
    Guid? WarehouseId,
    AdjustmentKind? Kind,
    InventoryAdjustmentStatus? Status,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Search);

[ApiController]
[Route("adjustments")]
[Tags(Tags.InventoryCounts)]
public sealed class GetInventoryAdjustmentsController(
    IQueryHandler<GetInventoryAdjustmentsQuery, PagedResult<InventoryAdjustmentResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<InventoryAdjustmentResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] GetInventoryAdjustmentsRequest request,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetInventoryAdjustmentsQuery(
            request.WarehouseId,
            request.Kind,
            request.Status,
            request.FromUtc,
            request.ToUtc,
            request.Search,
            pagination.Page,
            pagination.PageSize);
        Result<PagedResult<InventoryAdjustmentResponse>> result = await handler.Handle(query, cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
