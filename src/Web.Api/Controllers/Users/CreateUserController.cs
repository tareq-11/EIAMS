using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Users.Create;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[Route("admin/users")]
[Tags(Tags.Users)]
public sealed class CreateUserController(ICommandHandler<CreateUserCommand, CreateUserResponse> handler) : ControllerBase
{
    public sealed record RequestBody(
        string Email,
        string Username,
        string FirstName,
        string LastName,
        string Password,
        [property: JsonRequired] Guid RoleId,
        [property: JsonRequired] UserAssignmentScopeType ScopeType,
        Guid? ScopeId);

    [HttpPost]
    [RequestSizeLimit(AuthRequestLimits.MaximumBodySize)]
    [HasPermission(PermissionCodes.Users.Manage)]
    [ProducesResponseType<ApiResponse<CreateUserResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            request.Email,
            request.Username,
            request.FirstName,
            request.LastName,
            request.Password,
            request.RoleId,
            request.ScopeType,
            request.ScopeId);

        Result<CreateUserResponse> result = await handler.Handle(command, cancellationToken);
        return result.Match(
            value => ApiResults.Created(HttpContext, $"/api/v1/admin/users/{value.Id}", value),
            failure => CustomResults.Problem(failure, HttpContext));
    }
}
