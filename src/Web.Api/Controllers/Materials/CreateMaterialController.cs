using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Materials.Create;
using Domain.Materials;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Materials;

[ApiController]
[Route("materials")]
[Tags(Tags.Materials)]
public sealed class CreateMaterialController(ICommandHandler<CreateMaterialCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid FamilyId,
        string NameAr,
        string? NameEn,
        string Code,
        [property: JsonRequired] int MaterialKind,
        [property: JsonRequired] int TrackingType,
        [property: JsonRequired] bool HasExpiry,
        [property: JsonRequired] bool RequiresAssetNumber,
        string? Attributes);

    [HttpPost]
    [HasPermission(PermissionCodes.Materials.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateMaterialCommand(
            request.FamilyId,
            request.NameAr,
            request.NameEn,
            request.Code,
            (MaterialKind)request.MaterialKind,
            (TrackingType)request.TrackingType,
            request.HasExpiry,
            request.RequiresAssetNumber,
            request.Attributes);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
