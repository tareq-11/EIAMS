using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Assets.GetCurrentStatus;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Assets;

[ApiController]
[Route("assets/{assetId:guid}/current-status")]
[Tags(Tags.Assets)]
public sealed class GetAssetCurrentStatusController(
    IQueryHandler<GetAssetCurrentStatusQuery, AssetCurrentStatusResponse> handler) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    [ProducesResponseType<ApiResponse<AssetCurrentStatusResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid assetId, CancellationToken cancellationToken)
    {
        Result<AssetCurrentStatusResponse> result = await handler.Handle(
            new GetAssetCurrentStatusQuery(assetId),
            cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
