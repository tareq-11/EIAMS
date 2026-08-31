using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Assets;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Assets.GetById;

internal sealed class GetAssetByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService) : IQueryHandler<GetAssetByIdQuery, AssetDetailsResponse>
{
    public async Task<Result<AssetDetailsResponse>> Handle(
        GetAssetByIdQuery query,
        CancellationToken cancellationToken)
    {
        AssetDetailsResponse? asset = await (
                from entity in context.Assets.AsNoTracking()
                join current in context.AssetCurrentStatuses.AsNoTracking() on entity.Id equals current.AssetId
                join material in context.Materials.AsNoTracking() on entity.MaterialId equals material.Id
                join warehouse in context.Warehouses.AsNoTracking() on entity.WarehouseId equals warehouse.Id
                join line in context.DocumentLines.AsNoTracking() on entity.ReceiptLineId equals line.Id into receiptLines
                from line in receiptLines.DefaultIfEmpty()
                join document in context.WarehouseDocuments.AsNoTracking() on line.DocumentId equals document.Id into documents
                from document in documents.DefaultIfEmpty()
                where entity.Id == query.AssetId && entity.WarehouseId != null
                select new AssetDetailsResponse(
                    entity.Id,
                    entity.AssetNumber,
                    entity.SerialNumber,
                    material.Id,
                    material.Code,
                    material.NameAr,
                    material.NameEn,
                    entity.WarehouseId.GetValueOrDefault(),
                    warehouse.Code,
                    warehouse.Name,
                    current.CurrentStatus.ToString(),
                    current.ActiveCustodyId,
                    current.HolderType == null ? null : current.HolderType.ToString(),
                    current.HolderId,
                    current.CustodyKind == null ? null : current.CustodyKind.ToString(),
                    current.LatestMovementType == null ? null : current.LatestMovementType.ToString(),
                    current.LatestMovementAtUtc,
                    entity.ReceiptLineId,
                    document == null ? null : document.Id,
                    document == null ? null : document.SystemReferenceNumber,
                    entity.AcquisitionDate,
                    entity.WarrantyExpiry,
                    entity.RowVersion))
            .SingleOrDefaultAsync(cancellationToken);
        if (asset is null)
        {
            return Result.Failure<AssetDetailsResponse>(AssetErrors.NotFound(query.AssetId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.Assets.View, ScopeType.Warehouse, asset.WarehouseId, cancellationToken);
        return authorized
            ? asset
            : Result.Failure<AssetDetailsResponse>(AssetErrors.NotFound(query.AssetId));
    }
}
