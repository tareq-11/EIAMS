using Application.Abstractions.Messaging;
using Application.MaterialFamilies.GetById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialFamilies;

[ApiController]
[Route("material-families")]
[Tags(Tags.MaterialFamilies)]
public sealed class GetMaterialFamilyByIdController(
    IQueryHandler<GetMaterialFamilyByIdQuery, MaterialFamilyResponse> handler) : ControllerBase
{
    [HttpGet("{materialFamilyId:guid}")]
    [Authorize]
    public async Task<IResult> Handle(Guid materialFamilyId, CancellationToken cancellationToken)
    {
        var query = new GetMaterialFamilyByIdQuery(materialFamilyId);

        Result<MaterialFamilyResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
