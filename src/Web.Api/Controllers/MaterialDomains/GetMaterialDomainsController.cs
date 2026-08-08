using Application.Abstractions.Messaging;
using Application.MaterialDomains.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialDomains;

[ApiController]
[Route("material-domains")]
[Tags(Tags.MaterialDomains)]
public sealed class GetMaterialDomainsController(
    IQueryHandler<GetMaterialDomainsQuery, List<MaterialDomainResponse>> handler) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(Status? status, CancellationToken cancellationToken)
    {
        var query = new GetMaterialDomainsQuery(status);

        Result<List<MaterialDomainResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
