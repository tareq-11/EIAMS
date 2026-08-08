using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.Login;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[Route("users")]
[Tags(Tags.Users)]
public sealed class LoginController(ICommandHandler<LoginUserCommand, AccessTokensResponse> handler) : ControllerBase
{
    public sealed record RequestBody(string Email, string Password);

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new LoginUserCommand(request.Email, request.Password);

        Result<AccessTokensResponse> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
