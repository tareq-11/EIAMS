using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments.GetById;

internal sealed class GetWarehouseDocumentByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetWarehouseDocumentByIdQuery, WarehouseDocumentDetailsResponse>
{
    public async Task<Result<WarehouseDocumentDetailsResponse>> Handle(
        GetWarehouseDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        WarehouseDocumentDetailsResponse? document = await context.WarehouseDocuments
            .Where(d => d.Id == query.DocumentId)
            .Select(d => new WarehouseDocumentDetailsResponse
            {
                Id = d.Id,
                WarehouseId = d.WarehouseId,
                DocumentType = d.DocumentType.ToString(),
                PaperDocumentNumber = d.PaperDocumentNumber,
                PaperDocumentYear = d.PaperDocumentYear,
                SystemReferenceNumber = d.SystemReferenceNumber,
                SignedCopyAttachmentId = d.SignedCopyAttachmentId,
                DocumentStatus = d.DocumentStatus.ToString(),
                PostedBy = d.PostedBy,
                PostedAtUtc = d.PostedAtUtc,
                ReversalOfDocumentId = d.ReversalOfDocumentId,
                RowVersion = d.RowVersion
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return Result.Failure<WarehouseDocumentDetailsResponse>(WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<WarehouseDocumentDetailsResponse>(WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        document.ReversedByDocumentId = await context.WarehouseDocuments
            .Where(d => d.ReversalOfDocumentId == query.DocumentId)
            .Select(d => (Guid?)d.Id)
            .SingleOrDefaultAsync(cancellationToken);

        document.ReceivingInfo = await context.ReceivingInfos
            .AsNoTracking()
            .Where(info => info.Id == query.DocumentId)
            .Select(info => new ReceivingInfoResponse
            {
                SupplierRef = info.SupplierRef,
                SupplierInvoiceRef = info.SupplierInvoiceRef,
                ReceivingType = info.ReceivingType.ToString()
            })
            .SingleOrDefaultAsync(cancellationToken);

        document.IssueTo = await context.IssueTos
            .AsNoTracking()
            .Where(info => info.Id == query.DocumentId)
            .Select(info => new IssueToResponse
            {
                RecipientType = info.RecipientType.ToString(),
                RecipientId = info.RecipientId,
                IssueReason = info.IssueReason
            })
            .SingleOrDefaultAsync(cancellationToken);

        document.TransferInfo = await context.TransferInfos
            .AsNoTracking()
            .Where(info => info.Id == query.DocumentId)
            .Select(info => new TransferInfoResponse
            {
                DestinationWarehouseId = info.DestinationWarehouseId,
                TransferReason = info.TransferReason
            })
            .SingleOrDefaultAsync(cancellationToken);

        document.ReturnInfo = await context.ReturnInfos
            .AsNoTracking()
            .Where(info => info.Id == query.DocumentId)
            .Select(info => new ReturnInfoResponse
            {
                OriginalIssueDocumentId = info.OriginalIssueDocumentId,
                ReturnReason = info.ReturnReason
            })
            .SingleOrDefaultAsync(cancellationToken);

        document.InventoryAdjustment = await context.InventoryAdjustments
            .AsNoTracking()
            .Where(info => info.Id == query.DocumentId)
            .Select(info => new InventoryAdjustmentResponse
            {
                CountId = info.CountId,
                AdjustmentKind = info.AdjustmentKind.ToString(),
                Status = info.Status.ToString(),
                Reason = info.Reason
            })
            .SingleOrDefaultAsync(cancellationToken);

        document.Lines = await context.DocumentLines
            .Where(l => l.DocumentId == query.DocumentId)
            .Select(l => new DocumentLineResponse
            {
                Id = l.Id,
                SourceLineId = l.SourceLineId,
                MaterialId = l.MaterialId,
                LineType = l.LineType.ToString(),
                Quantity = l.Quantity,
                UnitId = l.UnitId,
                BaseQuantity = l.BaseQuantity,
                UnitPrice = l.UnitPrice,
                BatchNumber = l.BatchNumber,
                ExpiryDate = l.ExpiryDate,
                OpeningType = l.OpeningType == null ? null : l.OpeningType.ToString()
            })
            .ToListAsync(cancellationToken);

        Guid[] lineIds = document.Lines.Select(line => line.Id).ToArray();

        Dictionary<Guid, AdjustmentLineResponse> adjustmentsByLineId = await context.AdjustmentLines
            .AsNoTracking()
            .Where(item => lineIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => new AdjustmentLineResponse
            {
                Difference = item.Difference,
                Reason = item.Reason
            }, cancellationToken);

        foreach (DocumentLineResponse line in document.Lines)
        {
            if (adjustmentsByLineId.TryGetValue(line.Id, out AdjustmentLineResponse? adjustment))
            {
                line.Adjustment = adjustment;
            }
        }

        var assets = await context.Assets
            .AsNoTracking()
            .Where(asset => asset.ReceiptLineId != null && lineIds.Contains(asset.ReceiptLineId.Value))
            .Select(asset => new
            {
                LineId = asset.ReceiptLineId!.Value,
                Asset = new DocumentLineAssetResponse
                {
                    Id = asset.Id,
                    WarehouseId = asset.WarehouseId,
                    AssetNumber = asset.AssetNumber,
                    SerialNumber = asset.SerialNumber,
                    AcquisitionDate = asset.AcquisitionDate,
                    WarrantyExpiry = asset.WarrantyExpiry
                }
            })
            .ToListAsync(cancellationToken);

        var assetsByLineId = assets
            .GroupBy(item => item.LineId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Asset).ToList());

        foreach (DocumentLineResponse line in document.Lines)
        {
            if (assetsByLineId.TryGetValue(line.Id, out List<DocumentLineAssetResponse>? lineAssets))
            {
                line.Assets = lineAssets;
            }
        }

        var selectedAssets = await (
                from selection in context.DocumentLineAssetSelections.AsNoTracking()
                join asset in context.Assets.AsNoTracking() on selection.AssetId equals asset.Id
                where selection.DocumentId == query.DocumentId
                select new
                {
                    selection.DocumentLineId,
                    Selection = new DocumentLineSelectedAssetResponse
                    {
                        SelectionId = selection.Id,
                        AssetId = asset.Id,
                        AssetNumber = asset.AssetNumber,
                        SerialNumber = asset.SerialNumber
                    }
                })
            .ToListAsync(cancellationToken);

        var selectedAssetsByLineId = selectedAssets
            .GroupBy(item => item.DocumentLineId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Selection).ToList());

        foreach (DocumentLineResponse line in document.Lines)
        {
            if (selectedAssetsByLineId.TryGetValue(
                    line.Id,
                    out List<DocumentLineSelectedAssetResponse>? lineSelections))
            {
                line.SelectedAssets = lineSelections;
            }
        }

        document.Attachments = await context.DocumentAttachments
            .Where(a => a.DocumentId == query.DocumentId)
            .Select(a => new DocumentAttachmentResponse
            {
                Id = a.Id,
                AttachmentType = a.AttachmentType.ToString(),
                OriginalFilename = a.OriginalFilename,
                MimeType = a.MimeType,
                FileSize = a.FileSize,
                UploadedAtUtc = a.UploadedAtUtc
            })
            .ToListAsync(cancellationToken);

        return document;
    }
}
