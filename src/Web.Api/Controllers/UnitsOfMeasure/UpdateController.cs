using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UnitsOfMeasure.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UnitsOfMeasure;

[ApiController]
[Route("units-of-measure")]
[Tags(Tags.UnitsOfMeasure)]
public sealed class UpdateController(ICommandHandler<UpdateUnitOfMeasureCommand> handler) : ControllerBase
{
    public sealed record RequestBody(string Name, string Symbol, string UnitType);

    [HttpPut("{unitOfMeasureId:guid}")]
    [HasPermission(PermissionCodes.UnitsOfMeasure.Manage)]
    public async Task<IResult> Handle(Guid unitOfMeasureId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateUnitOfMeasureCommand(unitOfMeasureId, request.Name, request.Symbol, request.UnitType);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
