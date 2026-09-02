using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialDomains.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialDomains;

[ApiController]
[Route("catalog/domains")]
[Tags(Tags.MaterialDomains)]
public sealed class UpdateMaterialDomainController(ICommandHandler<UpdateMaterialDomainCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(string Name);

    [HttpPut("{domainId:guid}")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.MaterialDomains.Manage)]
    public async Task<IResult> Handle(Guid domainId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateMaterialDomainCommand(domainId, request.Name);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
