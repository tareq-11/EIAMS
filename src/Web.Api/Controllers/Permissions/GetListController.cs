using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Permissions.GetList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Permissions;

[ApiController]
[Route("permissions")]
[Tags(Tags.Permissions)]
public sealed class GetListController(IQueryHandler<GetPermissionsQuery, PagedResult<PermissionResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PermissionResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetPermissionsQuery(pagination.Page, pagination.PageSize);

        Result<PagedResult<PermissionResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
