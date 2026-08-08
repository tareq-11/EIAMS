using Application.Abstractions.Messaging;
using Application.MaterialDomains.GetById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialDomains;

[ApiController]
[Route("material-domains")]
[Tags(Tags.MaterialDomains)]
public sealed class GetMaterialDomainByIdController(
    IQueryHandler<GetMaterialDomainByIdQuery, MaterialDomainResponse> handler) : ControllerBase
{
    [HttpGet("{materialDomainId:guid}")]
    [Authorize]
    public async Task<IResult> Handle(Guid materialDomainId, CancellationToken cancellationToken)
    {
        var query = new GetMaterialDomainByIdQuery(materialDomainId);

        Result<MaterialDomainResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
