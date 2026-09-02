using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes.Revoke;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UserRoleScopes;

[ApiController]
[Route("admin/user-role-scopes")]
[Tags(Tags.UserRoleScopes)]
public sealed class RevokeController(ICommandHandler<RevokeUserRoleScopeCommand> handler) : ControllerBase
{
    [HttpDelete("{userRoleScopeId:guid}")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(Guid userRoleScopeId, CancellationToken cancellationToken)
    {
        var command = new RevokeUserRoleScopeCommand(userRoleScopeId);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
