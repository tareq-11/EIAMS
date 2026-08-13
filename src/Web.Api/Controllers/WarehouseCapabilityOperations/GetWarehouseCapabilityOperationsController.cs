using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.WarehouseCapabilityOperations.GetByCapability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseCapabilityOperations;

[ApiController]
[Route("warehouse-capabilities/{capabilityId:guid}/operations")]
[Tags(Tags.WarehouseCapabilityOperations)]
public sealed class GetWarehouseCapabilityOperationsController(
    IQueryHandler<GetWarehouseCapabilityOperationsQuery, PagedResult<WarehouseCapabilityOperationResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<WarehouseCapabilityOperationResponse>>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        Guid capabilityId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetWarehouseCapabilityOperationsQuery(
            capabilityId,
            pagination.Page,
            pagination.PageSize);

        Result<PagedResult<WarehouseCapabilityOperationResponse>> result = await handler.Handle(
            query,
            cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
