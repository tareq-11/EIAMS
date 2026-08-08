using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.RolePermissions.Remove;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("roles")]
[Tags(Tags.Roles)]
public sealed class RemovePermissionController(ICommandHandler<RemovePermissionFromRoleCommand> handler)
    : ControllerBase
{
    [HttpDelete("{roleId:guid}/permissions/{permissionId:guid}")]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(Guid roleId, Guid permissionId, CancellationToken cancellationToken)
    {
        var command = new RemovePermissionFromRoleCommand(roleId, permissionId);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
