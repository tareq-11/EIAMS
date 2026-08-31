using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Assets;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Assets.GetMovements;

internal sealed class GetAssetMovementsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetAssetMovementsQuery, PagedResult<AssetMovementResponse>>
{
    public async Task<Result<PagedResult<AssetMovementResponse>>> Handle(
        GetAssetMovementsQuery query,
        CancellationToken cancellationToken)
    {
        Guid? warehouseId = await context.Assets.AsNoTracking()
            .Where(asset => asset.Id == query.AssetId)
            .Select(asset => asset.WarehouseId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!warehouseId.HasValue || !await scopeAuthorizationService.HasPermissionInScopeAsync(
                userContext.UserId,
                PermissionCodes.Assets.View,
                ScopeType.Warehouse,
                warehouseId.Value,
                cancellationToken))
        {
            return Result.Failure<PagedResult<AssetMovementResponse>>(AssetErrors.NotFound(query.AssetId));
        }

        IQueryable<AssetMovementResponse> source =
            from movement in context.AssetMovementHistories.AsNoTracking()
            join document in context.WarehouseDocuments.AsNoTracking() on movement.DocumentId equals document.Id
            where movement.AssetId == query.AssetId
            orderby movement.MovedAtUtc descending, movement.Id descending
            select new AssetMovementResponse(
                movement.Id,
                movement.AssetId,
                movement.DocumentId,
                document.SystemReferenceNumber,
                document.DocumentType.ToString(),
                movement.MovementType.ToString(),
                movement.MovedAtUtc);
        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<AssetMovementResponse> items = await source.Skip(offset).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<AssetMovementResponse>(items, query.Page, query.PageSize, totalItems);
    }
}
