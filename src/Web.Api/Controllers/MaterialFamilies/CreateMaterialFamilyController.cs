using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialFamilies.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialFamilies;

[ApiController]
[Route("material-families")]
[Tags(Tags.MaterialFamilies)]
public sealed class CreateMaterialFamilyController(ICommandHandler<CreateMaterialFamilyCommand, Guid> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid CategoryId, string Name, string Code, [property: JsonRequired] Guid BaseUnitId);

    [HttpPost]
    [HasPermission(PermissionCodes.MaterialFamilies.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateMaterialFamilyCommand(
            request.CategoryId,
            request.Name,
            request.Code,
            request.BaseUnitId);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
