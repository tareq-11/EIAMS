using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Roles.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("roles")]
[Tags(Tags.Roles)]
public sealed class GetRoleByIdController(IQueryHandler<GetRoleByIdQuery, RoleResponse> handler) : ControllerBase
{
    [HttpGet("{roleId:guid}")]
    [ProducesResponseType<ApiResponse<RoleResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Roles.View)]
    public async Task<IResult> Handle(Guid roleId, CancellationToken cancellationToken)
    {
        var query = new GetRoleByIdQuery(roleId);

        Result<RoleResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
