using Application.Abstractions.Messaging;
using Application.Users.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[Route("users")]
[Tags(Tags.Users)]
public sealed class GetUserByIdController(IQueryHandler<GetUserByIdQuery, UserResponse> handler) : ControllerBase
{
    [HttpGet("{userId}")]
    [HasPermission(Permissions.UsersAccess)]
    public async Task<IResult> Handle(Guid userId, CancellationToken cancellationToken)
    {
        var query = new GetUserByIdQuery(userId);

        Result<UserResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
