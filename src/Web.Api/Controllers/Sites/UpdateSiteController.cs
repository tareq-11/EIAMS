using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Sites.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Sites;

[ApiController]
[Route("sites")]
[Tags(Tags.Sites)]
public sealed class UpdateSiteController(ICommandHandler<UpdateSiteCommand> handler) : ControllerBase
{
    public sealed record RequestBody(string Name, string? Location);

    [HttpPut("{siteId:guid}")]
    [HasPermission(PermissionCodes.Sites.Manage)]
    public async Task<IResult> Handle(Guid siteId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateSiteCommand(siteId, request.Name, request.Location);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
