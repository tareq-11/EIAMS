using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Sites.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Sites;

[ApiController]
[Route("sites")]
[Tags(Tags.Sites)]
public sealed class GetSitesController(IQueryHandler<GetSitesQuery, PagedResult<SiteResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SiteResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Guid? organizationId,
        Status? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetSitesQuery(organizationId, status, pagination.Page, pagination.PageSize);

        Result<PagedResult<SiteResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
