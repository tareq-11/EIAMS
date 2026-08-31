using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Custodies.GetCustodies;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Custodies;

[ApiController]
[Route("custodies")]
[Tags(Tags.Assets)]
public sealed class GetCustodiesController(
    IQueryHandler<GetCustodiesQuery, PagedResult<CustodyResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Custodies.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CustodyResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        [FromQuery] PartyType? holderType,
        [FromQuery] Guid? holderId,
        [FromQuery] Guid? materialId,
        [FromQuery] CustodySubjectType? subjectType,
        [FromQuery] string? status,
        [FromQuery] Guid? warehouseId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetCustodiesQuery(
            holderType,
            holderId,
            materialId,
            subjectType,
            status,
            warehouseId,
            pagination.Page,
            pagination.PageSize);

        Result<PagedResult<CustodyResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
