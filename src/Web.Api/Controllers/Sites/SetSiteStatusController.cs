using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Sites.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Sites;

[ApiController]
[Route("sites")]
[Tags(Tags.Sites)]
public sealed class SetSiteStatusController(ICommandHandler<SetSiteStatusCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{siteId:guid}/status")]
    [HasPermission(PermissionCodes.Sites.Manage)]
    public async Task<IResult> Handle(Guid siteId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetSiteStatusCommand(siteId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
