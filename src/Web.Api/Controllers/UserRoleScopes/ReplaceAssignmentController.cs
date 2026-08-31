using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes.Replace;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UserRoleScopes;

[ApiController]
[Route("users/{userId:guid}/role-scope")]
[Tags(Tags.UserRoleScopes)]
public sealed class ReplaceAssignmentController(
    ICommandHandler<ReplaceUserRoleScopeCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid RoleId,
        [property: JsonRequired] ScopeType ScopeType,
        Guid? ScopeId);

    [HttpPut]
    [HasPermission(PermissionCodes.Roles.Manage)]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid userId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new ReplaceUserRoleScopeCommand(
            userId,
            request.RoleId,
            request.ScopeType,
            request.ScopeId);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
