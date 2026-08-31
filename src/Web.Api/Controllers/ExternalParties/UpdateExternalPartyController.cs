using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.ExternalParties.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.ExternalParties;

[ApiController]
[Route("external-parties")]
[Tags(Tags.ExternalParties)]
public sealed class UpdateExternalPartyController(ICommandHandler<UpdateExternalPartyCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(
        string NameAr,
        string? Code,
        string? ContactInfo,
        string? Notes,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut("{externalPartyId:guid}")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Organizations.Manage)]
    public async Task<IResult> Handle(
        Guid externalPartyId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateExternalPartyCommand(
            externalPartyId,
            request.NameAr,
            request.Code,
            request.ContactInfo,
            request.Notes,
            request.ExpectedRowVersion);
        Result result = await handler.Handle(command, cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
