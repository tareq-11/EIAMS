using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Organizations.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Organizations;

[ApiController]
[Route("organizations")]
[Tags(Tags.Organizations)]
public sealed class GetOrganizationsController(
    IQueryHandler<GetOrganizationsQuery, PagedResult<OrganizationResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<OrganizationResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Status? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetOrganizationsQuery(status, pagination.Page, pagination.PageSize);

        Result<PagedResult<OrganizationResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
