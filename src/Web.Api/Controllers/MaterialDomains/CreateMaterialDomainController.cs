using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialDomains.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialDomains;

[ApiController]
[Route("material-domains")]
[Tags(Tags.MaterialDomains)]
public sealed class CreateMaterialDomainController(ICommandHandler<CreateMaterialDomainCommand, Guid> handler)
    : ControllerBase
{
    public sealed record RequestBody(string Name, string Code);

    [HttpPost]
    [HasPermission(PermissionCodes.MaterialDomains.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateMaterialDomainCommand(request.Name, request.Code);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
