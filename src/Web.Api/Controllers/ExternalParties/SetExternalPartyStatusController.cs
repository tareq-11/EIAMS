using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.ExternalParties.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.ExternalParties;

[ApiController]
[Route("external-parties")]
[Tags(Tags.ExternalParties)]
public sealed class SetExternalPartyStatusController(ICommandHandler<SetExternalPartyStatusCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] int Status,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut("{externalPartyId:guid}/status")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Organizations.Manage)]
    public async Task<IResult> Handle(
        Guid externalPartyId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new SetExternalPartyStatusCommand(
            externalPartyId, (Status)request.Status, request.ExpectedRowVersion);
        Result result = await handler.Handle(command, cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
