using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes.RemoveAssignment;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UserRoleScopes;

[ApiController]
[Route("admin/users/{userId:guid}/role-scope")]
[Tags(Tags.UserRoleScopes)]
public sealed class RemoveAssignmentController(
    ICommandHandler<RemoveUserRoleScopeCommand> handler) : ControllerBase
{
    [HttpDelete]
    [HasPermission(PermissionCodes.Roles.Manage)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(Guid userId, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new RemoveUserRoleScopeCommand(userId), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
