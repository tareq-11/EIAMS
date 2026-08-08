using Application.Abstractions.Messaging;
using Application.Permissions.GetList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Permissions;

[ApiController]
[Route("permissions")]
[Tags(Tags.Permissions)]
public sealed class GetListController(IQueryHandler<GetPermissionsQuery, List<PermissionResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(CancellationToken cancellationToken)
    {
        var query = new GetPermissionsQuery();

        Result<List<PermissionResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
