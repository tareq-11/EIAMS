using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes.GetAssignment;
using Application.UserRoleScopes.GetByUser;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UserRoleScopes;

[ApiController]
[Route("admin/users/{userId:guid}/role-scope")]
[Tags(Tags.UserRoleScopes)]
public sealed class GetAssignmentController(
    IQueryHandler<GetUserRoleScopeQuery, UserRoleScopeResponse> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Roles.View)]
    [ProducesResponseType<ApiResponse<UserRoleScopeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid userId, CancellationToken cancellationToken)
    {
        Result<UserRoleScopeResponse> result = await handler.Handle(
            new GetUserRoleScopeQuery(userId),
            cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
