using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Roles.GetList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("roles")]
[Tags(Tags.Roles)]
public sealed class GetRolesController(IQueryHandler<GetRolesQuery, PagedResult<RoleResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RoleResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetRolesQuery(pagination.Page, pagination.PageSize);

        Result<PagedResult<RoleResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
