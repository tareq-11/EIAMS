using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.Move;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialCategories;

[ApiController]
[Route("catalog/categories")]
[Tags(Tags.MaterialCategories)]
public sealed class MoveMaterialCategoryController(ICommandHandler<MoveMaterialCategoryCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(Guid? ParentCategoryId);

    [HttpPut("{categoryId:guid}/parent")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.MaterialCategories.Manage)]
    public async Task<IResult> Handle(Guid categoryId, RequestBody request, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new MoveMaterialCategoryCommand(categoryId, request.ParentCategoryId),
            cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
