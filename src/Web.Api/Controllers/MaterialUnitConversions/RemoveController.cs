using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialUnitConversions.Remove;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialUnitConversions;

[ApiController]
[Route("material-unit-conversions")]
[Tags(Tags.MaterialUnitConversions)]
public sealed class RemoveController(ICommandHandler<RemoveMaterialUnitConversionCommand> handler) : ControllerBase
{
    [HttpDelete("{materialUnitConversionId:guid}")]
    [HasPermission(PermissionCodes.Materials.Manage)]
    public async Task<IResult> Handle(Guid materialUnitConversionId, CancellationToken cancellationToken)
    {
        var command = new RemoveMaterialUnitConversionCommand(materialUnitConversionId);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
