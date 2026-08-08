using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialFamilies.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialFamilies;

[ApiController]
[Route("material-families")]
[Tags(Tags.MaterialFamilies)]
public sealed class SetMaterialFamilyStatusController(ICommandHandler<SetMaterialFamilyStatusCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{materialFamilyId:guid}/status")]
    [HasPermission(PermissionCodes.MaterialFamilies.Manage)]
    public async Task<IResult> Handle(Guid materialFamilyId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetMaterialFamilyStatusCommand(materialFamilyId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
