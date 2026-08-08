using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.RolePermissions.Assign;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("roles")]
[Tags(Tags.Roles)]
public sealed class AssignPermissionController(ICommandHandler<AssignPermissionToRoleCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid PermissionId);

    [HttpPost("{roleId:guid}/permissions")]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(Guid roleId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new AssignPermissionToRoleCommand(roleId, request.PermissionId);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
