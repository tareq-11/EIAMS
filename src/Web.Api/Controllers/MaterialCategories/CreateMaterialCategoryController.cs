using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.MaterialCategories;

[ApiController]
[Route("material-categories")]
[Tags(Tags.MaterialCategories)]
public sealed class CreateMaterialCategoryController(ICommandHandler<CreateMaterialCategoryCommand, Guid> handler)
    : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid MaterialDomainId, Guid? ParentCategoryId, string Name, string Code);

    [HttpPost]
    [HasPermission(PermissionCodes.MaterialCategories.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateMaterialCategoryCommand(
            request.MaterialDomainId,
            request.ParentCategoryId,
            request.Name,
            request.Code);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
