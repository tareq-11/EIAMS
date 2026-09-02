using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Roles.Create;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("admin/roles")]
[Tags(Tags.Roles)]
public sealed class CreateRoleController(ICommandHandler<CreateRoleCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(
        string Name,
        string? Description,
        [property: JsonRequired] IReadOnlyCollection<ScopeType> AllowedScopeTypes);

    [HttpPost]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateRoleCommand(request.Name, request.Description, request.AllowedScopeTypes);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
