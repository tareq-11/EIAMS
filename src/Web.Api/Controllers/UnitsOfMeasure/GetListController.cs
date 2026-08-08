using Application.Abstractions.Messaging;
using Application.UnitsOfMeasure.GetList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UnitsOfMeasure;

[ApiController]
[Route("units-of-measure")]
[Tags(Tags.UnitsOfMeasure)]
public sealed class GetListController(IQueryHandler<GetUnitsOfMeasureQuery, List<UnitOfMeasureResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(CancellationToken cancellationToken)
    {
        var query = new GetUnitsOfMeasureQuery();

        Result<List<UnitOfMeasureResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
