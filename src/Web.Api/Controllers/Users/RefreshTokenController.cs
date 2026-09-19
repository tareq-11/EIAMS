using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.GetSession;
using Application.Users.Login;
using Application.Users.Refresh;
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
public sealed class RefreshTokenController(
    ICommandHandler<RefreshTokenCommand, AccessTokensResponse> handler,
    IQueryHandler<GetUserSessionQuery, UserSessionResponse> sessionHandler,
    RefreshTokenTransport refreshTokenTransport)
    : ControllerBase
{
    public sealed record RequestBody(string? RefreshToken);

    [HttpPost("refresh")]
    [RequestSizeLimit(AuthRequestLimits.MaximumBodySize)]
    [ProducesResponseType<ApiResponse<AuthenticationTokensResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IResult> Handle(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RequestBody? request,
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

        if (result.IsFailure)
        {
            return CustomResults.Problem(result, HttpContext);
        }

        if (!string.IsNullOrWhiteSpace(result.Value.RefreshToken))
        {
            AuthCookies.SetRefreshTokenCookie(HttpContext, result.Value.RefreshToken);
        }

        // Include the authoritative session projection (D-AUTH-01 §13.2).
        Result<UserSessionResponse> sessionResult = await sessionHandler
            .Handle(new GetUserSessionQuery(result.Value.UserId), cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        UserSessionResponse? session = sessionResult.IsSuccess ? sessionResult.Value : null;
        return ApiResults.Ok(HttpContext, refreshTokenTransport.CreateResponse(result.Value, session));
    }
}
