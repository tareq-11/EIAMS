using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.RolePermissions.GetByRole;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("roles")]
[Tags(Tags.Roles)]
public sealed class GetPermissionsController(
    IQueryHandler<GetRolePermissionsQuery, PagedResult<PermissionResponse>> handler)
    : ControllerBase
{
    [HttpGet("{roleId:guid}/permissions")]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PermissionResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Guid roleId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetRolePermissionsQuery(roleId, pagination.Page, pagination.PageSize);

        Result<PagedResult<PermissionResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
