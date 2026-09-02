using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialFamilies.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialFamilies;

[ApiController]
[Route("catalog/families")]
[Tags(Tags.MaterialFamilies)]
public sealed class UpdateMaterialFamilyController(ICommandHandler<UpdateMaterialFamilyCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(string Name, string Code);

    [HttpPut("{familyId:guid}")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.MaterialFamilies.Manage)]
    public async Task<IResult> Handle(Guid familyId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateMaterialFamilyCommand(familyId, request.Name, request.Code);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
