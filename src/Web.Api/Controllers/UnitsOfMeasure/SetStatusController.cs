using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UnitsOfMeasure.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UnitsOfMeasure;

[ApiController]
[Route("catalog/units-of-measure")]
[Tags(Tags.UnitsOfMeasure)]
public sealed class SetStatusController(ICommandHandler<SetUnitOfMeasureStatusCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{unitId:guid}/status")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.UnitsOfMeasure.Manage)]
    public async Task<IResult> Handle(Guid unitId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetUnitOfMeasureStatusCommand(unitId, (Status)request.Status);
        Result result = await handler.Handle(command, cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
