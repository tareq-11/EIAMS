using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.InventoryAdjustments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryAdjustments.GetById;

internal sealed class GetInventoryAdjustmentByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetInventoryAdjustmentByIdQuery, InventoryAdjustmentDetailsResponse>
{
    public async Task<Result<InventoryAdjustmentDetailsResponse>> Handle(
        GetInventoryAdjustmentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var header = await (
                from adjustment in context.InventoryAdjustments.AsNoTracking()
                join document in context.WarehouseDocuments.AsNoTracking() on adjustment.Id equals document.Id
                join warehouse in context.Warehouses.AsNoTracking() on document.WarehouseId equals warehouse.Id
                where adjustment.Id == query.AdjustmentId
                select new { Adjustment = adjustment, Document = document, Warehouse = warehouse })
            .SingleOrDefaultAsync(cancellationToken);
        if (header is null)
        {
            return Result.Failure<InventoryAdjustmentDetailsResponse>(
                InventoryAdjustmentErrors.NotFound(query.AdjustmentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            header.Document.WarehouseId,
            cancellationToken);
        if (!authorized)
        {
            return Result.Failure<InventoryAdjustmentDetailsResponse>(
                InventoryAdjustmentErrors.NotFound(query.AdjustmentId));
        }

        List<InventoryAdjustmentLineResponse> lines = await (
                from detail in context.AdjustmentLines.AsNoTracking()
                join line in context.DocumentLines.AsNoTracking() on detail.Id equals line.Id
                join material in context.Materials.AsNoTracking() on line.MaterialId equals material.Id
                where detail.AdjustmentId == query.AdjustmentId
                orderby material.Code, line.Id
                select new InventoryAdjustmentLineResponse(
                    line.Id,
                    material.Id,
                    material.Code,
                    material.NameAr,
                    line.Quantity,
                    detail.Difference,
                    detail.Reason,
                    (from selection in context.DocumentLineAssetSelections.AsNoTracking()
                     join asset in context.Assets.AsNoTracking() on selection.AssetId equals asset.Id
                     where selection.DocumentId == query.AdjustmentId && selection.DocumentLineId == line.Id
                     orderby asset.AssetNumber
                     select new AdjustmentAssetResponse(asset.Id, asset.AssetNumber, asset.SerialNumber)).ToList()))
            .ToListAsync(cancellationToken);

        return new InventoryAdjustmentDetailsResponse(
            header.Adjustment.Id,
            header.Document.WarehouseId,
            header.Warehouse.Code,
            header.Warehouse.Name,
            header.Adjustment.CountId,
            header.Adjustment.AdjustmentKind.ToString(),
            header.Adjustment.Status.ToString(),
            header.Adjustment.Reason,
            header.Document.SystemReferenceNumber,
            header.Document.DocumentStatus.ToString(),
            header.Adjustment.CreatedAtUtc,
            header.Document.PostedAtUtc,
            header.Document.RowVersion,
            lines);
    }
}
