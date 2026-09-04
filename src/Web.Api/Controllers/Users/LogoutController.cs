using Application.Abstractions.Messaging;
using Application.Users.Logout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[AllowAnonymous]
[Route("auth")]
[Tags(Tags.Users)]
public sealed class LogoutController(ICommandHandler<LogoutUserCommand> handler) : ControllerBase
{
    public sealed record RequestBody(string? RefreshToken);

    [HttpPost("logout")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IResult> Handle(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RequestBody? request,
        CancellationToken cancellationToken)
    {
        string? token = AuthCookies.GetRefreshTokenFromCookieOrBody(HttpContext, request?.RefreshToken);

        var command = new LogoutUserCommand(token);

        Result result = await handler.Handle(command, cancellationToken);

        AuthCookies.ClearRefreshTokenCookies(HttpContext);

        return result.ToApiResponse(HttpContext);
    }
}
