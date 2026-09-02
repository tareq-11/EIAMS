using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialCategories;

[ApiController]
[Route("catalog/categories")]
[Tags(Tags.MaterialCategories)]
public sealed class GetMaterialCategoryByIdController(
    IQueryHandler<GetMaterialCategoryByIdQuery, MaterialCategoryResponse> handler) : ControllerBase
{
    [HttpGet("{categoryId:guid}")]
    [ProducesResponseType<ApiResponse<MaterialCategoryResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Materials.View)]
    public async Task<IResult> Handle(Guid categoryId, CancellationToken cancellationToken)
    {
        var query = new GetMaterialCategoryByIdQuery(categoryId);

        Result<MaterialCategoryResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
