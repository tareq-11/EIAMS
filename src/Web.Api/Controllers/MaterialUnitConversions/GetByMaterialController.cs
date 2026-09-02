using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.MaterialUnitConversions.GetByMaterial;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialUnitConversions;

[ApiController]
[Route("catalog/materials/{materialId:guid}/unit-conversions")]
[Tags(Tags.MaterialUnitConversions)]
public sealed class GetByMaterialController(
    IQueryHandler<GetMaterialUnitConversionsQuery, PagedResult<MaterialUnitConversionResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Materials.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MaterialUnitConversionResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Guid materialId,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetMaterialUnitConversionsQuery(materialId, pagination.Page, pagination.PageSize);

        Result<PagedResult<MaterialUnitConversionResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}
