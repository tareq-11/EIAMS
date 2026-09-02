using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialFamilies.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialFamilies;

[ApiController]
[Route("catalog/families")]
[Tags(Tags.MaterialFamilies)]
public sealed class SetMaterialFamilyStatusController(ICommandHandler<SetMaterialFamilyStatusCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{familyId:guid}/status")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.MaterialFamilies.Manage)]
    public async Task<IResult> Handle(Guid familyId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetMaterialFamilyStatusCommand(familyId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
