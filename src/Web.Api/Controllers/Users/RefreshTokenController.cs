using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.Refresh;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[Route("auth")]
[Tags(Tags.Users)]
public sealed class RefreshTokenController(ICommandHandler<RefreshTokenCommand, AccessTokensResponse> handler)
    : ControllerBase
{
    public sealed record RequestBody(string? RefreshToken);

    [HttpPost("refresh")]
    [ProducesResponseType<ApiResponse<AccessTokensResponse>>(StatusCodes.Status200OK)]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IResult> Handle(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RequestBody? request,
        CancellationToken cancellationToken)
    {
        string? token = AuthCookies.GetRefreshTokenFromCookieOrBody(HttpContext, request?.RefreshToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status400BadRequest,
                Domain.Users.UserErrors.InvalidRefreshToken.Code,
                Domain.Users.UserErrors.InvalidRefreshToken.Description);
        }

        var command = new RefreshTokenCommand(token);

        Result<AccessTokensResponse> result = await handler.Handle(command, cancellationToken);

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value.RefreshToken))
        {
            AuthCookies.SetRefreshTokenCookie(HttpContext, result.Value.RefreshToken);
        }

        return result.ToApiResponse(HttpContext);
    }
}
