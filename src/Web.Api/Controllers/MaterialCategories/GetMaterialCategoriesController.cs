using Application.Abstractions.Messaging;
using Application.MaterialCategories.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialCategories;

[ApiController]
[Route("material-categories")]
[Tags(Tags.MaterialCategories)]
public sealed class GetMaterialCategoriesController(
    IQueryHandler<GetMaterialCategoriesQuery, List<MaterialCategoryResponse>> handler) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(
        Guid? materialDomainId,
        Guid? parentCategoryId,
        bool rootOnly,
        Status? status,
        CancellationToken cancellationToken)
    {
        var query = new GetMaterialCategoriesQuery(materialDomainId, parentCategoryId, rootOnly, status);

        Result<List<MaterialCategoryResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
