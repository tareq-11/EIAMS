using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialCategories;

[ApiController]
[Route("catalog/categories")]
[Tags(Tags.MaterialCategories)]
public sealed class SetMaterialCategoryStatusController(ICommandHandler<SetMaterialCategoryStatusCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{categoryId:guid}/status")]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.MaterialCategories.Manage)]
    public async Task<IResult> Handle(Guid categoryId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetMaterialCategoryStatusCommand(categoryId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
