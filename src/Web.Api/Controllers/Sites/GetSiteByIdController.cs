using Application.Abstractions.Messaging;
using Application.Sites.GetById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Sites;

[ApiController]
[Route("sites")]
[Tags(Tags.Sites)]
public sealed class GetSiteByIdController(IQueryHandler<GetSiteByIdQuery, SiteResponse> handler) : ControllerBase
{
    [HttpGet("{siteId:guid}")]
    [Authorize]
    public async Task<IResult> Handle(Guid siteId, CancellationToken cancellationToken)
    {
        var query = new GetSiteByIdQuery(siteId);

        Result<SiteResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
