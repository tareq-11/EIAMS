using System.Text.Json.Serialization;
using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Users.Update;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

/// <summary>
/// Account metadata: display name and email.
/// </summary>
/// <remarks>
/// This body no longer binds <c>username</c> or <c>status</c>. The login is immutable
/// after creation, and suspension is <c>PUT /admin/users/{userId}/status</c>.
/// <para>
/// Measured behaviour, verified against the running API: a body that still carries
/// <c>username</c> is accepted and the field is SILENTLY IGNORED, leaving the stored
/// login unchanged. Safe, but not loudly rejected — this model binder does not enforce
/// <c>additionalProperties:false</c> the way the envelope-bound bodies elsewhere in the
/// API do. A client that believed it had renamed an account would be wrong with no
/// error. Left as a follow-up rather than fixed here, because closing it means deciding
/// for the whole API whether unknown properties are rejected or ignored.
/// </para>
/// </remarks>
[ApiController]
[Route("admin/users")]
[Tags(Tags.Users)]
public sealed class UpdateUserController(ICommandHandler<UpdateUserCommand> handler) : ControllerBase
{
    public sealed record RequestBody(
        string Email,
        string FirstName,
        string LastName,
        // Required: the metadata save is conditional on the version it was read at.
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut("{userId:guid}")]
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
        var command = new UpdateUserCommand(
            userId,
            request.Email,
            request.FirstName,
            request.LastName,
            request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}