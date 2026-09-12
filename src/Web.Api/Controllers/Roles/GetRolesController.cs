using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Roles.GetList;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("admin/roles")]
[Tags(Tags.Roles)]
public sealed class GetRolesController(IQueryHandler<GetRolesQuery, PagedResult<RoleResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Roles.View)]
    [ProducesResponseType<ApiResponse<PagedData<RoleResponse>>>(StatusCodes.Status200OK)]
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
