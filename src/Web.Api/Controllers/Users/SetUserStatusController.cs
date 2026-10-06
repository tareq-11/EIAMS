using System.Text.Json.Serialization;
using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Users.SetStatus;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

/// <summary>
/// Account lifecycle transition, kept separate from the metadata update.
/// </summary>
/// <remarks>
/// Suspending an account used to go through <c>PUT /admin/users/{userId}</c>, which
/// required resubmitting the user's entire profile — including a <c>username</c> that
/// no read projection returned. That made suspension impossible to perform without
/// first being able to see the login it would otherwise have to guess. It is a
/// security transition with its own guards (no self-suspension, never the last
/// Enterprise Administrator, revoking every live refresh token), so it owns its route.
/// </remarks>
[ApiController]
[Route("admin/users/{userId:guid}/status")]
[Tags(Tags.Users)]
public sealed class SetUserStatusController(
    ICommandHandler<SetUserStatusCommand> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] UserStatus Status,
        // Required: the transition is conditional on the version it was read at, so a
        // suspension cannot silently overwrite a concurrent profile or status change.
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut]
    [HasPermission(PermissionCodes.Users.Manage)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
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
        var command = new SetUserStatusCommand(userId, request.Status, request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}