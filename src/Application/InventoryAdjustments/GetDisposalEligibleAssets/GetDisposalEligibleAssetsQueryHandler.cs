using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Abstractions.Searching;
using Domain.Assets;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryAdjustments.GetDisposalEligibleAssets;

internal sealed class GetDisposalEligibleAssetsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetDisposalEligibleAssetsQuery, PagedResult<DisposalEligibleAssetResponse>>
{
    public async Task<Result<PagedResult<DisposalEligibleAssetResponse>>> Handle(
        GetDisposalEligibleAssetsQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId, PermissionCodes.Assets.View, cancellationToken);
        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<DisposalEligibleAssetResponse>>(Error.Forbidden(
                "Assets.Forbidden", "The current user cannot view assets in any warehouse."));
        }

        Guid[] warehouseIds = access.WarehouseIds.ToArray();
        string? search = SqlLikePattern.CreateContains(query.Search, normalizeToUpper: true);
        IQueryable<DisposalEligibleAssetResponse> source =
            from asset in context.Assets.AsNoTracking()
            join status in context.AssetCurrentStatuses.AsNoTracking() on asset.Id equals status.AssetId
            join material in context.Materials.AsNoTracking() on asset.MaterialId equals material.Id
            join warehouse in context.Warehouses.AsNoTracking() on status.WarehouseId equals warehouse.Id
            where status.WarehouseId != null
            where access.HasEnterpriseAccess || warehouseIds.Contains(warehouse.Id)
            where query.WarehouseId == null || status.WarehouseId == query.WarehouseId
            where query.MaterialId == null || asset.MaterialId == query.MaterialId
            where status.CurrentStatus == AssetCurrentStatus.InStock ||
                  status.CurrentStatus == AssetCurrentStatus.Issued ||
                  status.CurrentStatus == AssetCurrentStatus.InCustody
            where !(
                from selection in context.DocumentLineAssetSelections.AsNoTracking()
                join adjustment in context.InventoryAdjustments.AsNoTracking() on selection.DocumentId equals adjustment.Id
                join document in context.WarehouseDocuments.AsNoTracking() on adjustment.Id equals document.Id
                where selection.AssetId == asset.Id && adjustment.AdjustmentKind == AdjustmentKind.Disposal &&
                      (document.DocumentStatus == DocumentStatus.Draft || document.DocumentStatus == DocumentStatus.Submitted)
                select selection.Id).Any()
#pragma warning disable CA1304, CA1311 // Translated by EF Core into SQL UPPER.
            where search == null || EF.Functions.Like(asset.AssetNumber.ToUpper(), search, SqlLikePattern.EscapeCharacter) ||
                  asset.SerialNumber != null && EF.Functions.Like(asset.SerialNumber.ToUpper(), search, SqlLikePattern.EscapeCharacter) ||
                  EF.Functions.Like(material.Code.ToUpper(), search, SqlLikePattern.EscapeCharacter) ||
                  EF.Functions.Like(material.NameAr.ToUpper(), search, SqlLikePattern.EscapeCharacter)
#pragma warning restore CA1304, CA1311
            orderby asset.AssetNumber, asset.Id
            select new DisposalEligibleAssetResponse(
                asset.Id,
                asset.AssetNumber,
                asset.SerialNumber,
                material.Id,
                material.Code,
                material.NameAr,
                warehouse.Id,
                warehouse.Code,
                warehouse.Name,
                status.CurrentStatus.ToString(),
                asset.RowVersion);

        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<DisposalEligibleAssetResponse> items = await source.Skip(offset).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<DisposalEligibleAssetResponse>(items, query.Page, query.PageSize, totalItems);
    }
}
