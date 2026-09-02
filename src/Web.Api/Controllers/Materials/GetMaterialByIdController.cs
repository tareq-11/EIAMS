using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Materials.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Materials;

[ApiController]
[Route("catalog/materials")]
[Tags(Tags.Materials)]
public sealed class GetMaterialByIdController(IQueryHandler<GetMaterialByIdQuery, MaterialResponse> handler)
    : ControllerBase
{
    [HttpGet("{materialId:guid}")]
    [ProducesResponseType<ApiResponse<MaterialResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Materials.View)]
    public async Task<IResult> Handle(Guid materialId, CancellationToken cancellationToken)
    {
        var query = new GetMaterialByIdQuery(materialId);

        Result<MaterialResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
