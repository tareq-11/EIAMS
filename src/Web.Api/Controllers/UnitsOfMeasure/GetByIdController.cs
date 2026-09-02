using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UnitsOfMeasure.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UnitsOfMeasure;

[ApiController]
[Route("catalog/units-of-measure")]
[Tags(Tags.UnitsOfMeasure)]
public sealed class GetByIdController(IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureResponse> handler)
    : ControllerBase
{
    [HttpGet("{unitId:guid}")]
    [ProducesResponseType<ApiResponse<UnitOfMeasureResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.UnitsOfMeasure.View)]
    public async Task<IResult> Handle(Guid unitId, CancellationToken cancellationToken)
    {
        var query = new GetUnitOfMeasureByIdQuery(unitId);

        Result<UnitOfMeasureResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
