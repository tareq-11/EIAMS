using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Users.GetList;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[Route("admin/users")]
[Tags(Tags.Users)]
public sealed class GetUsersController(
    IQueryHandler<GetUsersQuery, PagedResult<UserAdministrationResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Users.Access)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<UserAdministrationResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        string? search,
        UserStatus? status,
        bool? hasRoleScope,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetUsersQuery(
            search,
            status,
            hasRoleScope,
            pagination.Page,
            pagination.PageSize);

        Result<PagedResult<UserAdministrationResponse>> result = await handler.Handle(query, cancellationToken);
        return result.ToPaginatedApiResponse(HttpContext);
    }
}
