using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.OrganizationalUnits.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.OrganizationalUnits;

[ApiController]
[Route("organizational-units")]
[Tags(Tags.OrganizationalUnits)]
public sealed class UpdateOrganizationalUnitController(ICommandHandler<UpdateOrganizationalUnitCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(string Name, string UnitType);

    [HttpPut("{organizationalUnitId:guid}")]
    [HasPermission(PermissionCodes.OrganizationalUnits.Manage)]
    public async Task<IResult> Handle(
        Guid organizationalUnitId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOrganizationalUnitCommand(organizationalUnitId, request.Name, request.UnitType);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
