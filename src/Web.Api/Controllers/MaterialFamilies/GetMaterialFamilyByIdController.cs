using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialFamilies.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialFamilies;

[ApiController]
[Route("catalog/families")]
[Tags(Tags.MaterialFamilies)]
public sealed class GetMaterialFamilyByIdController(
    IQueryHandler<GetMaterialFamilyByIdQuery, MaterialFamilyResponse> handler) : ControllerBase
{
    [HttpGet("{familyId:guid}")]
    [ProducesResponseType<ApiResponse<MaterialFamilyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Materials.View)]
    public async Task<IResult> Handle(Guid familyId, CancellationToken cancellationToken)
    {
        var query = new GetMaterialFamilyByIdQuery(familyId);

        Result<MaterialFamilyResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
