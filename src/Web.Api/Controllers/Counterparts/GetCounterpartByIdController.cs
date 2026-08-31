using Application.Abstractions.Messaging;
using Application.Abstractions.Recipients;
using Application.Abstractions.Authorization;
using Application.Counterparts.GetById;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Counterparts;

[ApiController]
[Route("counterparts")]
[Tags(Tags.Counterparts)]
public sealed class GetCounterpartByIdController(
    IQueryHandler<GetCounterpartByIdQuery, CounterpartResolution> handler)
    : ControllerBase
{
    [HttpGet("{type}/{counterpartId:guid}")]
    [ProducesResponseType<ApiResponse<CounterpartResolution>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    public async Task<IResult> Handle(
        PartyType type,
        Guid counterpartId,
        CancellationToken cancellationToken)
    {
        Result<CounterpartResolution> result = await handler.Handle(
            new GetCounterpartByIdQuery(type, counterpartId), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
