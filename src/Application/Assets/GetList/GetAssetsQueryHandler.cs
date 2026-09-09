using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Abstractions.Searching;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Assets.GetList;

internal sealed class GetAssetsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetAssetsQuery, PagedResult<AssetResponse>>
{
    public async Task<Result<PagedResult<AssetResponse>>> Handle(
        GetAssetsQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId, PermissionCodes.Assets.View, cancellationToken);
        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<AssetResponse>>(Error.Forbidden(
                "Assets.Forbidden", "The current user cannot view assets in any warehouse."));
        }

        Guid[] warehouseIds = access.WarehouseIds.ToArray();
        string? search = SqlLikePattern.CreateContains(query.Search, normalizeToUpper: true);
        IQueryable<AssetResponse> source =
            from asset in context.Assets.AsNoTracking()
            join current in context.AssetCurrentStatuses.AsNoTracking() on asset.Id equals current.AssetId
            join material in context.Materials.AsNoTracking() on asset.MaterialId equals material.Id
            join warehouse in context.Warehouses.AsNoTracking() on asset.WarehouseId equals warehouse.Id
            where asset.WarehouseId != null
            where access.HasEnterpriseAccess || warehouseIds.Contains(warehouse.Id)
            where query.WarehouseId == null || asset.WarehouseId == query.WarehouseId
            where query.MaterialId == null || asset.MaterialId == query.MaterialId
            where query.Status == null || current.CurrentStatus == query.Status
#pragma warning disable CA1304, CA1311 // Translated by EF Core into SQL UPPER.
            where search == null || EF.Functions.Like(asset.AssetNumber.ToUpper(), search, SqlLikePattern.EscapeCharacter) ||
                  asset.SerialNumber != null && EF.Functions.Like(asset.SerialNumber.ToUpper(), search, SqlLikePattern.EscapeCharacter) ||
                  EF.Functions.Like(material.Code.ToUpper(), search, SqlLikePattern.EscapeCharacter) ||
                  EF.Functions.Like(material.NameAr.ToUpper(), search, SqlLikePattern.EscapeCharacter)
#pragma warning restore CA1304, CA1311
            orderby asset.AssetNumber, asset.Id
            select new AssetResponse(
                asset.Id,
                asset.AssetNumber,
                asset.SerialNumber,
                material.Id,
                material.Code,
                material.NameAr,
                warehouse.Id,
                warehouse.Code,
                warehouse.Name,
                current.CurrentStatus.ToString(),
                current.ActiveCustodyId,
#pragma warning disable IDE0031 // Null propagation is not supported in expression trees.
                current.HolderType == null ? null : current.HolderType.ToString(),
#pragma warning restore IDE0031
                current.HolderId,
                asset.AcquisitionDate,
                asset.WarrantyExpiry,
                asset.RowVersion);

        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<AssetResponse> items = await source.Skip(offset).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<AssetResponse>(items, query.Page, query.PageSize, totalItems);
    }
}
