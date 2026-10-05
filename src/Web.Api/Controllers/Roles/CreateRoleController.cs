using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Roles;
using Application.Roles.Create;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

/// <summary>
/// Creates a role together with its complete initial permission set (RESOLUTION-027 S16/S17).
/// <para>
/// This is deliberately the one create in the API that does not answer with a bare
/// <c>ResourceIdResponse</c>, and the one that answers <c>201</c>. Role plus grants form a single
/// atomic unit, so the create result <em>is</em> the aggregate; returning only an identifier would
/// force the client straight into a follow-up read to learn what it just created.
/// </para>
/// </summary>
[ApiController]
[Route("admin/roles")]
[Tags(Tags.Roles)]
public sealed class CreateRoleController(ICommandHandler<CreateRoleCommand, RoleResponse> handler) : ControllerBase
{
    public sealed record RequestBody(
        string Name,
        string NameAr,
        string? Description,
        [property: JsonRequired] IReadOnlyCollection<string> PermissionCodes,
        [property: JsonRequired] IReadOnlyCollection<UserAssignmentScopeType> AllowedScopeTypes);

    [HttpPost]
    [ProducesResponseType<ApiResponse<RoleResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateRoleCommand(
            request.Name,
            request.NameAr,
            request.Description,
            request.PermissionCodes,
            request.AllowedScopeTypes.Select(scopeType => scopeType.ToPersistedScopeType()).ToArray());

        Result<RoleResponse> result = await handler.Handle(command, cancellationToken);

        return result.ToCreatedApiResponse(HttpContext, role => $"/api/v1/admin/roles/{role.Id}");
    }
}