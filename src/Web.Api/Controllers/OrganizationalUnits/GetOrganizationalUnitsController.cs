using Application.Abstractions.Messaging;
using Application.OrganizationalUnits.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.OrganizationalUnits;

[ApiController]
[Route("organizational-units")]
[Tags(Tags.OrganizationalUnits)]
public sealed class GetOrganizationalUnitsController(
    IQueryHandler<GetOrganizationalUnitsQuery, List<OrganizationalUnitResponse>> handler) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(
        Guid? siteId,
        Guid? parentId,
        Status? status,
        CancellationToken cancellationToken)
    {
        var query = new GetOrganizationalUnitsQuery(siteId, parentId, status);

        Result<List<OrganizationalUnitResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
