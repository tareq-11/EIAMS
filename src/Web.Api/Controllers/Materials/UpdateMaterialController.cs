using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Materials.Update;
using Domain.Materials;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Materials;

[ApiController]
[Route("materials")]
[Tags(Tags.Materials)]
public sealed class UpdateMaterialController(ICommandHandler<UpdateMaterialCommand> handler) : ControllerBase
{
    public sealed record RequestBody(
        string NameAr,
        string? NameEn,
        [property: JsonRequired] int MaterialKind,
        [property: JsonRequired] int TrackingType,
        [property: JsonRequired] bool HasExpiry,
        [property: JsonRequired] bool RequiresAssetNumber,
        string? Attributes);

    [HttpPut("{materialId:guid}")]
    [HasPermission(PermissionCodes.Materials.Manage)]
    public async Task<IResult> Handle(Guid materialId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateMaterialCommand(
            materialId,
            request.NameAr,
            request.NameEn,
            (MaterialKind)request.MaterialKind,
            (TrackingType)request.TrackingType,
            request.HasExpiry,
            request.RequiresAssetNumber,
            request.Attributes);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
