using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Assets;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Assets.GetCurrentStatus;

internal sealed class GetAssetCurrentStatusQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetAssetCurrentStatusQuery, AssetCurrentStatusResponse>
{
    public async Task<Result<AssetCurrentStatusResponse>> Handle(
        GetAssetCurrentStatusQuery query,
        CancellationToken cancellationToken)
    {
        AssetCurrentStatusView? view = await context.AssetCurrentStatuses.AsNoTracking()
            .SingleOrDefaultAsync(item => item.AssetId == query.AssetId, cancellationToken);

        if (view?.WarehouseId is null)
        {
            return Result.Failure<AssetCurrentStatusResponse>(AssetErrors.NotFound(query.AssetId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            view.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<AssetCurrentStatusResponse>(AssetErrors.NotFound(query.AssetId));
        }

        return new AssetCurrentStatusResponse(
            view.AssetId,
            view.MaterialId,
            view.WarehouseId,
            view.AssetNumber,
            view.SerialNumber,
            view.CurrentStatus.ToString(),
            view.ActiveCustodyId,
            view.HolderType?.ToString(),
            view.HolderId,
            view.CustodyKind?.ToString(),
            view.LatestMovementType?.ToString(),
            view.LatestMovementAtUtc);
    }
}
