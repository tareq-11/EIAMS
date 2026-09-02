using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialDomains.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialDomains;

[ApiController]
[Route("catalog/domains")]
[Tags(Tags.MaterialDomains)]
public sealed class SetMaterialDomainStatusController(ICommandHandler<SetMaterialDomainStatusCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{domainId:guid}/status")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.MaterialDomains.Manage)]
    public async Task<IResult> Handle(Guid domainId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetMaterialDomainStatusCommand(domainId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
