using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.UserRoleScopes.GetByUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UserRoleScopes;

[ApiController]
[Route("users/{userId:guid}/role-scopes")]
[Tags(Tags.UserRoleScopes)]
public sealed class GetByUserController(
    IQueryHandler<GetUserRoleScopesQuery, PagedResult<UserRoleScopeResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<UserRoleScopeResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Guid userId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetUserRoleScopesQuery(userId, pagination.Page, pagination.PageSize);

        Result<PagedResult<UserRoleScopeResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
