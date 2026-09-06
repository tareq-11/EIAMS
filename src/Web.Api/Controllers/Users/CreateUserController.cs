using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Users.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[Route("admin/users")]
[Tags(Tags.Users)]
public sealed class CreateUserController(ICommandHandler<CreateUserCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(string Email, string FirstName, string LastName, string Password);
    public sealed record ResponseBody(Guid Id);

    [HttpPost]
    [RequestSizeLimit(AuthRequestLimits.MaximumBodySize)]
    [HasPermission(PermissionCodes.Users.Access)]
    [ProducesResponseType<ApiResponse<ResponseBody>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password);

        Result<Guid> result = await handler.Handle(command, cancellationToken);
        return result.Match(
            id => ApiResults.Created(HttpContext, $"/api/v1/admin/users/{id}", new ResponseBody(id)),
            failure => CustomResults.Problem(failure, HttpContext));
    }
}
