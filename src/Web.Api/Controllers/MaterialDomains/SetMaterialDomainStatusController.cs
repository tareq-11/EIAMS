using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialDomains.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialDomains;

[ApiController]
[Route("material-domains")]
[Tags(Tags.MaterialDomains)]
public sealed class SetMaterialDomainStatusController(ICommandHandler<SetMaterialDomainStatusCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{materialDomainId:guid}/status")]
    [HasPermission(PermissionCodes.MaterialDomains.Manage)]
    public async Task<IResult> Handle(Guid materialDomainId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetMaterialDomainStatusCommand(materialDomainId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
