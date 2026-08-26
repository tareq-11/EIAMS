using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.OrganizationalUnits.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.OrganizationalUnits;

[ApiController]
[Route("organizational-units")]
[Tags(Tags.OrganizationalUnits)]
public sealed class GetOrganizationalUnitsController(
    IQueryHandler<GetOrganizationalUnitsQuery, PagedResult<OrganizationalUnitResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.OrganizationalUnits.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<OrganizationalUnitResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Guid? siteId,
        Guid? parentId,
        Status? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetOrganizationalUnitsQuery(
            siteId,
            parentId,
            status,
            pagination.Page,
            pagination.PageSize);

        Result<PagedResult<OrganizationalUnitResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
