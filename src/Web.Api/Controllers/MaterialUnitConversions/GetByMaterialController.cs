using Application.Abstractions.Messaging;
using Application.MaterialUnitConversions.GetByMaterial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialUnitConversions;

[ApiController]
[Route("materials/{materialId:guid}/unit-conversions")]
[Tags(Tags.MaterialUnitConversions)]
public sealed class GetByMaterialController(
    IQueryHandler<GetMaterialUnitConversionsQuery, List<MaterialUnitConversionResponse>> handler) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(Guid materialId, CancellationToken cancellationToken)
    {
        var query = new GetMaterialUnitConversionsQuery(materialId);

        Result<List<MaterialUnitConversionResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
