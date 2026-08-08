using Application.Abstractions.Messaging;
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
    IQueryHandler<GetMaterialFamiliesQuery, List<MaterialFamilyResponse>> handler) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(Guid? categoryId, Status? status, CancellationToken cancellationToken)
    {
        var query = new GetMaterialFamiliesQuery(categoryId, status);

        Result<List<MaterialFamilyResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
