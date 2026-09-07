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
public sealed class LogoutController(
    ICommandHandler<LogoutUserCommand> handler,
    RefreshTokenTransport refreshTokenTransport) : ControllerBase
{
    public sealed record RequestBody(string? RefreshToken);

    [HttpPost("logout")]
    [RequestSizeLimit(AuthRequestLimits.MaximumBodySize)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IResult> Handle(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RequestBody? request,
        CancellationToken cancellationToken)
    {
        RefreshTokenResolution resolution = refreshTokenTransport.Resolve(HttpContext, request?.RefreshToken);

        if (!resolution.IsAccepted)
        {
            return ApiResults.Error(
                HttpContext,
                resolution.ErrorStatusCode,
                resolution.ErrorCode!,
                resolution.ErrorMessage!);
        }

        string? token = resolution.Token;

        var command = new LogoutUserCommand(token);

        Result result = await handler.Handle(command, cancellationToken);

        AuthCookies.ClearRefreshTokenCookies(HttpContext);

        return result.ToApiResponse(HttpContext);
    }
}
