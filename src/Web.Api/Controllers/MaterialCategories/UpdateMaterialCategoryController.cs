using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialCategories;

[ApiController]
[Route("material-categories")]
[Tags(Tags.MaterialCategories)]
public sealed class UpdateMaterialCategoryController(ICommandHandler<UpdateMaterialCategoryCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(string Name, string Code);

    [HttpPut("{materialCategoryId:guid}")]
    [HasPermission(PermissionCodes.MaterialCategories.Manage)]
    public async Task<IResult> Handle(Guid materialCategoryId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateMaterialCategoryCommand(materialCategoryId, request.Name, request.Code);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
