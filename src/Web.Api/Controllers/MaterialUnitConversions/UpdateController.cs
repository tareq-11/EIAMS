using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialUnitConversions.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialUnitConversions;

[ApiController]
[Route("catalog/materials/{materialId:guid}/unit-conversions")]
[Tags(Tags.MaterialUnitConversions)]
public sealed class UpdateController(
    ICommandHandler<UpdateMaterialUnitConversionCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] decimal Factor);

    [HttpPut("{conversionId:guid}")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Materials.Manage)]
    public async Task<IResult> Handle(
        Guid materialId,
        Guid conversionId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateMaterialUnitConversionCommand(materialId, conversionId, request.Factor);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
