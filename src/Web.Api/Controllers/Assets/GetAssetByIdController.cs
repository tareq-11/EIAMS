using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Assets.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Assets;

[ApiController]
[Route("assets/{assetId:guid}")]
[Tags(Tags.Assets)]
public sealed class GetAssetByIdController(IQueryHandler<GetAssetByIdQuery, AssetDetailsResponse> handler)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Assets.View)]
    [ProducesResponseType<ApiResponse<AssetDetailsResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid assetId, CancellationToken cancellationToken)
    {
        Result<AssetDetailsResponse> result = await handler.Handle(new GetAssetByIdQuery(assetId), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}
