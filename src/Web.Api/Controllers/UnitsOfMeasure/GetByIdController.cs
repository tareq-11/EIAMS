using Application.Abstractions.Messaging;
using Application.UnitsOfMeasure.GetById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UnitsOfMeasure;

[ApiController]
[Route("units-of-measure")]
[Tags(Tags.UnitsOfMeasure)]
public sealed class GetByIdController(IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureResponse> handler)
    : ControllerBase
{
    [HttpGet("{unitOfMeasureId:guid}")]
    [Authorize]
    public async Task<IResult> Handle(Guid unitOfMeasureId, CancellationToken cancellationToken)
    {
        var query = new GetUnitOfMeasureByIdQuery(unitOfMeasureId);

        Result<UnitOfMeasureResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
