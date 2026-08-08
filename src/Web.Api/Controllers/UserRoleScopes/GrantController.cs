using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes.Grant;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UserRoleScopes;

[ApiController]
[Route("user-role-scopes")]
[Tags(Tags.UserRoleScopes)]
public sealed class GrantController(ICommandHandler<GrantUserRoleScopeCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid UserId, [property: JsonRequired] Guid RoleId, [property: JsonRequired] int ScopeType, Guid? ScopeId);

    [HttpPost]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new GrantUserRoleScopeCommand(
            request.UserId,
            request.RoleId,
            (ScopeType)request.ScopeType,
            request.ScopeId);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
