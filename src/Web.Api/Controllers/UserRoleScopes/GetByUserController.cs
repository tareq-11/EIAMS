using Application.Abstractions.Messaging;
using Application.UserRoleScopes.GetByUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.UserRoleScopes;

[ApiController]
[Route("users/{userId:guid}/role-scopes")]
[Tags(Tags.UserRoleScopes)]
public sealed class GetByUserController(IQueryHandler<GetUserRoleScopesQuery, List<UserRoleScopeResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(Guid userId, CancellationToken cancellationToken)
    {
        var query = new GetUserRoleScopesQuery(userId);

        Result<List<UserRoleScopeResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
