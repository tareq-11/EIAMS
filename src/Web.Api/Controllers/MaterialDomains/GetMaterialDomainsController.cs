using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.MaterialDomains.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialDomains;

[ApiController]
[Route("material-domains")]
[Tags(Tags.MaterialDomains)]
public sealed class GetMaterialDomainsController(
    IQueryHandler<GetMaterialDomainsQuery, PagedResult<MaterialDomainResponse>> handler) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MaterialDomainResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Status? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetMaterialDomainsQuery(status, pagination.Page, pagination.PageSize);

        Result<PagedResult<MaterialDomainResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
