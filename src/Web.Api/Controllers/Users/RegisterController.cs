using Application.Abstractions.Messaging;
using Application.Users.Register;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[Route("users")]
[Tags(Tags.Users)]
public sealed class RegisterController(ICommandHandler<RegisterUserCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(string Email, string FirstName, string LastName, string Password);

    public sealed record RegisterUserResponse(Guid Id);

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [ProducesResponseType<ApiResponse<RegisterUserResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(request.Email, request.FirstName, request.LastName, request.Password);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.Match(
            userId => ApiResults.Created(HttpContext, $"/users/{userId}", new RegisterUserResponse(userId)),
            failure => CustomResults.Problem(failure, HttpContext));
    }
}
