using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.Refresh;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[Route("users")]
[Tags(Tags.Users)]
public sealed class RefreshTokenController(ICommandHandler<RefreshTokenCommand, AccessTokensResponse> handler)
    : ControllerBase
{
    public sealed record RequestBody(string RefreshToken);

    [HttpPost("refresh-token")]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);

        Result<AccessTokensResponse> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
