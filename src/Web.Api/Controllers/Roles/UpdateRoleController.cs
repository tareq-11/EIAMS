using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Roles;
using Application.Roles.Update;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("admin/roles")]
[Tags(Tags.Roles)]
public sealed class UpdateRoleController(ICommandHandler<UpdateRoleCommand, RoleResponse> handler) : ControllerBase
{
    public sealed record RequestBody(
        string Name,
        string NameAr,
        string? Description,
        [property: JsonRequired] int ExpectedRowVersion,
        [property: JsonRequired] IReadOnlyCollection<UserAssignmentScopeType> AllowedScopeTypes);

    [HttpPut("{roleId:guid}")]
    [ProducesResponseType<ApiResponse<RoleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(Guid roleId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateRoleCommand(
            roleId,
            request.Name,
            request.NameAr,
            request.Description,
            request.ExpectedRowVersion,
            request.AllowedScopeTypes.Select(scopeType => scopeType.ToPersistedScopeType()).ToArray());

        Result<RoleResponse> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}