using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.ExternalParties;
using Application.ExternalParties.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.ExternalParties;

[ApiController]
[Route("external-parties")]
[Tags(Tags.ExternalParties)]
public sealed class GetExternalPartiesController(
    IQueryHandler<GetExternalPartiesQuery, PagedResult<ExternalPartyResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedData<ExternalPartyResponse>>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Organizations.View)]
    public async Task<IResult> Handle(
        string? search,
        Status? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetExternalPartiesQuery(search, status, pagination.Page, pagination.PageSize);
        Result<PagedResult<ExternalPartyResponse>> result = await handler.Handle(query, cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
