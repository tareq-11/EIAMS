using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.ExternalParties.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.ExternalParties;

[ApiController]
[Route("external-parties")]
[Tags(Tags.ExternalParties)]
public sealed class CreateExternalPartyController(ICommandHandler<CreateExternalPartyCommand, Guid> handler)
    : ControllerBase
{
    public sealed record RequestBody(string NameAr, string? Code, string? ContactInfo, string? Notes);

    [HttpPost]
    [ProducesResponseType<ApiResponse<ResourceIdResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Organizations.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateExternalPartyCommand(
            request.NameAr, request.Code, request.ContactInfo, request.Notes);
        Result<Guid> result = await handler.Handle(command, cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
