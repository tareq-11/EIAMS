using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Materials.GetList;
using Domain.Materials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Materials;

[ApiController]
[Route("materials")]
[Tags(Tags.Materials)]
public sealed class GetMaterialsController(IQueryHandler<GetMaterialsQuery, PagedResult<MaterialResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MaterialResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Guid? familyId,
        Guid? materialDomainId,
        MaterialStatus? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetMaterialsQuery(
            familyId,
            materialDomainId,
            status,
            pagination.Page,
            pagination.PageSize);

        Result<PagedResult<MaterialResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
