using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[AllowAnonymous]
[Route("auth")]
[Tags(Tags.Users)]
public sealed class LoginController(
    ICommandHandler<LoginUserCommand, AccessTokensResponse> handler,
    RefreshTokenTransport refreshTokenTransport) : ControllerBase
{
    public sealed record RequestBody(string Username, string Password);

    [HttpPost("login")]
    [RequestSizeLimit(AuthRequestLimits.MaximumBodySize)]
    [ProducesResponseType<ApiResponse<AuthenticationTokensResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        // Login also sets a credential cookie, so apply the same origin policy before
        // invoking the handler. Requests without Origin remain valid for non-browser clients.
        RefreshTokenResolution origin = refreshTokenTransport.ValidateCookieOrigin(HttpContext);
        if (!origin.IsAccepted)
        {
            return ApiResults.Error(
                HttpContext,
                origin.ErrorStatusCode,
                origin.ErrorCode!,
                origin.ErrorMessage!);
        }

        var command = new LoginUserCommand(request.Username, request.Password);

        Result<AccessTokensResponse> result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return CustomResults.Problem(result, HttpContext);
        }

        if (!string.IsNullOrWhiteSpace(result.Value.RefreshToken))
        {
            AuthCookies.SetRefreshTokenCookie(HttpContext, result.Value.RefreshToken);
        }

        return ApiResults.Ok(HttpContext, refreshTokenTransport.CreateResponse(result.Value));
    }
}
