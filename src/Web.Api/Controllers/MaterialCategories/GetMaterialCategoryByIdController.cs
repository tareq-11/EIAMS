using Application.Abstractions.Messaging;
using Application.MaterialCategories.GetById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialCategories;

[ApiController]
[Route("material-categories")]
[Tags(Tags.MaterialCategories)]
public sealed class GetMaterialCategoryByIdController(
    IQueryHandler<GetMaterialCategoryByIdQuery, MaterialCategoryResponse> handler) : ControllerBase
{
    [HttpGet("{materialCategoryId:guid}")]
    [Authorize]
    public async Task<IResult> Handle(Guid materialCategoryId, CancellationToken cancellationToken)
    {
        var query = new GetMaterialCategoryByIdQuery(materialCategoryId);

        Result<MaterialCategoryResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
