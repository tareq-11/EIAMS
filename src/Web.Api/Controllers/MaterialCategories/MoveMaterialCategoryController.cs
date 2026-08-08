using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.Move;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialCategories;

[ApiController]
[Route("material-categories")]
[Tags(Tags.MaterialCategories)]
public sealed class MoveMaterialCategoryController(ICommandHandler<MoveMaterialCategoryCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(Guid? ParentCategoryId);

    [HttpPut("{materialCategoryId:guid}/parent")]
    [HasPermission(PermissionCodes.MaterialCategories.Manage)]
    public async Task<IResult> Handle(Guid materialCategoryId, RequestBody request, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new MoveMaterialCategoryCommand(materialCategoryId, request.ParentCategoryId),
            cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
