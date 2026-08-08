using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialUnitConversions.Add;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialUnitConversions;

[ApiController]
[Route("materials/{materialId:guid}/unit-conversions")]
[Tags(Tags.MaterialUnitConversions)]
public sealed class AddController(ICommandHandler<AddMaterialUnitConversionCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid FromUnitId, [property: JsonRequired] Guid ToBaseUnitId, [property: JsonRequired] decimal Factor);

    [HttpPost]
    [HasPermission(PermissionCodes.Materials.Manage)]
    public async Task<IResult> Handle(Guid materialId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new AddMaterialUnitConversionCommand(
            materialId,
            request.FromUnitId,
            request.ToBaseUnitId,
            request.Factor);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
