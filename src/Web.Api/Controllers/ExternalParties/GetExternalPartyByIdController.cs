using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.ExternalParties;
using Application.ExternalParties.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.ExternalParties;

[ApiController]
[Route("external-parties")]
[Tags(Tags.ExternalParties)]
public sealed class GetExternalPartyByIdController(
    IQueryHandler<GetExternalPartyByIdQuery, ExternalPartyResponse> handler)
    : ControllerBase
{
    [HttpGet("{externalPartyId:guid}")]
    [ProducesResponseType<ApiResponse<ExternalPartyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Organizations.View)]
    public async Task<IResult> Handle(Guid externalPartyId, CancellationToken cancellationToken)
    {
        Result<ExternalPartyResponse> result = await handler.Handle(
            new GetExternalPartyByIdQuery(externalPartyId), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
