using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Roles.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("roles")]
[Tags(Tags.Roles)]
public sealed class CreateRoleController(ICommandHandler<CreateRoleCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(string Name, string? Description);

    [HttpPost]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateRoleCommand(request.Name, request.Description);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
