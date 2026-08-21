using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Custodies.Assign;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Custodies;

[ApiController]
[Route("assets/{assetId:guid}/custody-assignment")]
[Tags(Tags.Assets)]
public sealed class AssignAssetCustodyController(
    ICommandHandler<AssignAssetCustodyCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] Guid EmployeeId,
        [property: JsonRequired] int ExpectedCustodyRowVersion,
        string? Note);

    [HttpPost]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid assetId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new AssignAssetCustodyCommand(
            assetId,
            request.EmployeeId,
            request.ExpectedCustodyRowVersion,
            request.Note);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToCreatedApiResponse(HttpContext, custodyId => $"/assets/{assetId}/custodies/{custodyId}");
    }
}
