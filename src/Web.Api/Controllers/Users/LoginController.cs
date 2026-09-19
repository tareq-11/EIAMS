using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.GetSession;
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
    IQueryHandler<GetUserSessionQuery, UserSessionResponse> sessionHandler,
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

        // Include the authoritative session projection so the SPA can populate the
        // session cache synchronously after login without a second round-trip (D-AUTH-01
        // §13.2: the contract is "session inside the login response").
        Result<UserSessionResponse> sessionResult = await sessionHandler
            .Handle(new GetUserSessionQuery(result.Value.UserId), cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        UserSessionResponse? session = sessionResult.IsSuccess ? sessionResult.Value : null;
        return ApiResults.Ok(HttpContext, refreshTokenTransport.CreateResponse(result.Value, session));
    }
}
