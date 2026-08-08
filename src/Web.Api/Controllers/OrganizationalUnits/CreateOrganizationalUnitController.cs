using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.OrganizationalUnits.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.OrganizationalUnits;

[ApiController]
[Route("organizational-units")]
[Tags(Tags.OrganizationalUnits)]
public sealed class CreateOrganizationalUnitController(ICommandHandler<CreateOrganizationalUnitCommand, Guid> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid SiteId, Guid? ParentId, string Name, string UnitType);

    [HttpPost]
    [HasPermission(PermissionCodes.OrganizationalUnits.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateOrganizationalUnitCommand(
            request.SiteId,
            request.ParentId,
            request.Name,
            request.UnitType);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
