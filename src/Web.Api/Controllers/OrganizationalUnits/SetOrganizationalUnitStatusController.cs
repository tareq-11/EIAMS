using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.OrganizationalUnits.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.OrganizationalUnits;

[ApiController]
[Route("organizational-units")]
[Tags(Tags.OrganizationalUnits)]
public sealed class SetOrganizationalUnitStatusController(ICommandHandler<SetOrganizationalUnitStatusCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{organizationalUnitId:guid}/status")]
    [HasPermission(PermissionCodes.OrganizationalUnits.Manage)]
    public async Task<IResult> Handle(
        Guid organizationalUnitId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new SetOrganizationalUnitStatusCommand(organizationalUnitId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
