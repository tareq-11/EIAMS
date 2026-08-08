using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Organizations.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Organizations;

[ApiController]
[Route("organizations")]
[Tags(Tags.Organizations)]
public sealed class CreateOrganizationController(ICommandHandler<CreateOrganizationCommand, Guid> handler)
    : ControllerBase
{
    public sealed record RequestBody(string Name, string Code);

    [HttpPost]
    [HasPermission(PermissionCodes.Organizations.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateOrganizationCommand(request.Name, request.Code);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
