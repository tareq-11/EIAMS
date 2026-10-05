using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Roles;
using Application.RolePermissions.Replace;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

/// <summary>
/// Wholesale replacement of a role's permission set (RESOLUTION-027 S20/S23).
/// <para>
/// This replaces the retired one-at-a-time <c>POST</c>/<c>DELETE</c> pair. Replacement is
/// all-or-nothing and shares the role aggregate version with metadata updates, so the caller must
/// send the <c>rowVersion</c> it read from any role projection.
/// </para>
/// </summary>
[ApiController]
[Route("admin/roles")]
[Tags(Tags.Roles)]
public sealed class ReplaceRolePermissionsController(
    ICommandHandler<ReplaceRolePermissionsCommand, RoleResponse> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] IReadOnlyCollection<string> PermissionCodes,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut("{roleId:guid}/permissions")]
    [ProducesResponseType<ApiResponse<RoleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(Guid roleId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new ReplaceRolePermissionsCommand(
            roleId,
            request.PermissionCodes,
            request.ExpectedRowVersion);

        Result<RoleResponse> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}