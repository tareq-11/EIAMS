using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialUnitConversions.GetById;
using Application.MaterialUnitConversions.GetByMaterial;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialUnitConversions;

[ApiController]
[Route("materials/{materialId:guid}/unit-conversions")]
[Tags(Tags.MaterialUnitConversions)]
public sealed class GetByIdController(
    IQueryHandler<GetMaterialUnitConversionByIdQuery, MaterialUnitConversionResponse> handler)
    : ControllerBase
{
    [HttpGet("{conversionId:guid}")]
    [HasPermission(PermissionCodes.Materials.View)]
    [ProducesResponseType<ApiResponse<MaterialUnitConversionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(
        Guid materialId,
        Guid conversionId,
        CancellationToken cancellationToken)
    {
        var query = new GetMaterialUnitConversionByIdQuery(materialId, conversionId);

        Result<MaterialUnitConversionResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
