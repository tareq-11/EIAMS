using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.MaterialFamilies.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialFamilies;

[ApiController]
[Route("material-families")]
[Tags(Tags.MaterialFamilies)]
public sealed class GetMaterialFamiliesController(
    IQueryHandler<GetMaterialFamiliesQuery, PagedResult<MaterialFamilyResponse>> handler) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MaterialFamilyResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Guid? categoryId,
        Status? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetMaterialFamiliesQuery(categoryId, status, pagination.Page, pagination.PageSize);

        Result<PagedResult<MaterialFamilyResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
