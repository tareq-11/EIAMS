using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialDomains.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialDomains;

[ApiController]
[Route("catalog/domains")]
[Tags(Tags.MaterialDomains)]
public sealed class GetMaterialDomainByIdController(
    IQueryHandler<GetMaterialDomainByIdQuery, MaterialDomainResponse> handler) : ControllerBase
{
    [HttpGet("{domainId:guid}")]
    [ProducesResponseType<ApiResponse<MaterialDomainResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Materials.View)]
    public async Task<IResult> Handle(Guid domainId, CancellationToken cancellationToken)
    {
        var query = new GetMaterialDomainByIdQuery(domainId);

        Result<MaterialDomainResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
