using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes.Revoke;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UserRoleScopes;

[ApiController]
[Route("user-role-scopes")]
[Tags(Tags.UserRoleScopes)]
public sealed class RevokeController(ICommandHandler<RevokeUserRoleScopeCommand> handler) : ControllerBase
{
    [HttpDelete("{userRoleScopeId:guid}")]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(Guid userRoleScopeId, CancellationToken cancellationToken)
    {
        var command = new RevokeUserRoleScopeCommand(userRoleScopeId);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
