using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Sites.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Sites;

[ApiController]
[Route("sites")]
[Tags(Tags.Sites)]
public sealed class CreateSiteController(ICommandHandler<CreateSiteCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid OrganizationId, string Name, string Code, string? Location);

    [HttpPost]
    [HasPermission(PermissionCodes.Sites.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateSiteCommand(request.OrganizationId, request.Name, request.Code, request.Location);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
