using System.Text.Json.Serialization;
using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Users.Update;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[Route("users")]
[Tags(Tags.Users)]
public sealed class UpdateUserController(ICommandHandler<UpdateUserCommand> handler) : ControllerBase
{
    public sealed record RequestBody(
        string Email,
        string FirstName,
        string LastName,
        [property: JsonRequired] UserStatus Status);

    [HttpPut("{userId:guid}")]
    [HasPermission(PermissionCodes.Users.Access)]
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
            request.Status);

        Result result = await handler.Handle(command, cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
