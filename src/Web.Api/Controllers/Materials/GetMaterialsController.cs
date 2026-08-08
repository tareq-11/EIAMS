using Application.Abstractions.Messaging;
using Application.Materials.GetList;
using Domain.Materials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Materials;

[ApiController]
[Route("materials")]
[Tags(Tags.Materials)]
public sealed class GetMaterialsController(IQueryHandler<GetMaterialsQuery, List<MaterialResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(
        Guid? familyId,
        Guid? materialDomainId,
        MaterialStatus? status,
        CancellationToken cancellationToken)
    {
        var query = new GetMaterialsQuery(familyId, materialDomainId, status);

        Result<List<MaterialResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
