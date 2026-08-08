using Application.Abstractions.Messaging;
using Application.RolePermissions.GetByRole;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("roles")]
[Tags(Tags.Roles)]
public sealed class GetPermissionsController(IQueryHandler<GetRolePermissionsQuery, List<PermissionResponse>> handler)
    : ControllerBase
{
    [HttpGet("{roleId:guid}/permissions")]
    [Authorize]
    public async Task<IResult> Handle(Guid roleId, CancellationToken cancellationToken)
    {
        var query = new GetRolePermissionsQuery(roleId);

        Result<List<PermissionResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
