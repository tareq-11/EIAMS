using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.MaterialCategories.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialCategories;

[ApiController]
[Route("catalog/categories")]
[Tags(Tags.MaterialCategories)]
public sealed class GetMaterialCategoriesController(
    IQueryHandler<GetMaterialCategoriesQuery, PagedResult<MaterialCategoryResponse>> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Materials.View)]
    [ProducesResponseType<ApiResponse<PagedData<MaterialCategoryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Guid? materialDomainId,
        Guid? parentCategoryId,
        bool rootOnly,
        Status? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetMaterialCategoriesQuery(
            materialDomainId,
            parentCategoryId,
            rootOnly,
            status,
            pagination.Page,
            pagination.PageSize);

        Result<PagedResult<MaterialCategoryResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
