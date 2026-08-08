using Application.Abstractions.Messaging;
using Application.Sites.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Sites;

[ApiController]
[Route("sites")]
[Tags(Tags.Sites)]
public sealed class GetSitesController(IQueryHandler<GetSitesQuery, List<SiteResponse>> handler) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(Guid? organizationId, Status? status, CancellationToken cancellationToken)
    {
        var query = new GetSitesQuery(organizationId, status);

        Result<List<SiteResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
