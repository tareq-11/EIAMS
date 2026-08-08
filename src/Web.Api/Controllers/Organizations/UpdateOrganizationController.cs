using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Organizations.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Organizations;

[ApiController]
[Route("organizations")]
[Tags(Tags.Organizations)]
public sealed class UpdateOrganizationController(ICommandHandler<UpdateOrganizationCommand> handler) : ControllerBase
{
    public sealed record RequestBody(string Name);

    [HttpPut("{organizationId:guid}")]
    [HasPermission(PermissionCodes.Organizations.Manage)]
    public async Task<IResult> Handle(Guid organizationId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateOrganizationCommand(organizationId, request.Name);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
