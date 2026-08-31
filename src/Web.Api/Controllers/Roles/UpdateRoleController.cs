using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Roles.Update;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Roles;

[ApiController]
[Route("roles")]
[Tags(Tags.Roles)]
public sealed class UpdateRoleController(ICommandHandler<UpdateRoleCommand> handler) : ControllerBase
{
    public sealed record RequestBody(
        string Name,
        string? Description,
        [property: JsonRequired] IReadOnlyCollection<ScopeType> AllowedScopeTypes);

    [HttpPut("{roleId:guid}")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Roles.Manage)]
    public async Task<IResult> Handle(Guid roleId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateRoleCommand(
            roleId,
            request.Name,
            request.Description,
            request.AllowedScopeTypes);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
