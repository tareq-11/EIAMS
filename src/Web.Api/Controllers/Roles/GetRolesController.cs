using Application.Abstractions.Messaging;
using Application.Roles.GetList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("roles")]
[Tags(Tags.Roles)]
public sealed class GetRolesController(IQueryHandler<GetRolesQuery, List<RoleResponse>> handler) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(CancellationToken cancellationToken)
    {
        var query = new GetRolesQuery();

        Result<List<RoleResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
