using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialCategories;

[ApiController]
[Route("material-categories")]
[Tags(Tags.MaterialCategories)]
public sealed class SetMaterialCategoryStatusController(ICommandHandler<SetMaterialCategoryStatusCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{materialCategoryId:guid}/status")]
    [HasPermission(PermissionCodes.MaterialCategories.Manage)]
    public async Task<IResult> Handle(Guid materialCategoryId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetMaterialCategoryStatusCommand(materialCategoryId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
