using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Users.GetSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[Authorize]
[ApiController]
[Route("users")]
[Tags(Tags.Users)]
public sealed class GetSessionController(
    IQueryHandler<GetUserSessionQuery, UserSessionResponse> handler,
    IUserContext userContext) : ControllerBase
{
    [HttpGet("session")]
    [ProducesResponseType<ApiResponse<UserSessionResponse>>(StatusCodes.Status200OK)]
    public async Task<IResult> Handle(CancellationToken cancellationToken)
    {
        var query = new GetUserSessionQuery(userContext.UserId);

        Result<UserSessionResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
