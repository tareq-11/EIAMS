using Application.Abstractions.Messaging;
using Application.Organizations.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Organizations;

[ApiController]
[Route("organizations")]
[Tags(Tags.Organizations)]
public sealed class GetOrganizationsController(IQueryHandler<GetOrganizationsQuery, List<OrganizationResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(Status? status, CancellationToken cancellationToken)
    {
        var query = new GetOrganizationsQuery(status);

        Result<List<OrganizationResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
