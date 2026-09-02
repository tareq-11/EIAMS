using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes.Grant;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UserRoleScopes;

[ApiController]
[Route("admin/user-role-scopes")]
[Tags(Tags.UserRoleScopes)]
public sealed class GrantController(ICommandHandler<GrantUserRoleScopeCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid UserId,
        [property: JsonRequired] Guid RoleId,
        [property: JsonRequired] ScopeType ScopeType,
        Guid? ScopeId);

    [HttpPost]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        Response.Headers["Deprecation"] = "true";
        Response.Headers.Append("Link", $"</users/{request.UserId}/role-scope>; rel=successor-version");

        var command = new GrantUserRoleScopeCommand(
            request.UserId,
            request.RoleId,
            request.ScopeType,
            request.ScopeId);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
