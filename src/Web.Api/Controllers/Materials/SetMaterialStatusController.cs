using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Materials.SetStatus;
using Domain.Materials;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Materials;

[ApiController]
[Route("materials")]
[Tags(Tags.Materials)]
public sealed class SetMaterialStatusController(ICommandHandler<SetMaterialStatusCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{materialId:guid}/status")]
    [HasPermission(PermissionCodes.Materials.Manage)]
    public async Task<IResult> Handle(Guid materialId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetMaterialStatusCommand(materialId, (MaterialStatus)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
