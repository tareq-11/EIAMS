using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UnitsOfMeasure.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UnitsOfMeasure;

[ApiController]
[Route("units-of-measure")]
[Tags(Tags.UnitsOfMeasure)]
public sealed class CreateController(ICommandHandler<CreateUnitOfMeasureCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(string Name, string Symbol, string UnitType);

    [HttpPost]
    [HasPermission(PermissionCodes.UnitsOfMeasure.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateUnitOfMeasureCommand(request.Name, request.Symbol, request.UnitType);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
