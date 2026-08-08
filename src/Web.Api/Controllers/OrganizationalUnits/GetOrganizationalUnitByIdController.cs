using Application.Abstractions.Messaging;
using Application.OrganizationalUnits.GetById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.OrganizationalUnits;

[ApiController]
[Route("organizational-units")]
[Tags(Tags.OrganizationalUnits)]
public sealed class GetOrganizationalUnitByIdController(
    IQueryHandler<GetOrganizationalUnitByIdQuery, OrganizationalUnitResponse> handler) : ControllerBase
{
    [HttpGet("{organizationalUnitId:guid}")]
    [Authorize]
    public async Task<IResult> Handle(Guid organizationalUnitId, CancellationToken cancellationToken)
    {
        var query = new GetOrganizationalUnitByIdQuery(organizationalUnitId);

        Result<OrganizationalUnitResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
