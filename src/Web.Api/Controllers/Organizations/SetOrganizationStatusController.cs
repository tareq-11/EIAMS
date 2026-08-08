using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Organizations.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Organizations;

[ApiController]
[Route("organizations")]
[Tags(Tags.Organizations)]
public sealed class SetOrganizationStatusController(ICommandHandler<SetOrganizationStatusCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{organizationId:guid}/status")]
    [HasPermission(PermissionCodes.Organizations.Manage)]
    public async Task<IResult> Handle(Guid organizationId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetOrganizationStatusCommand(organizationId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
