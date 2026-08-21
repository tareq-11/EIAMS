using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Application.Abstractions.Warehouses;
using Domain.Assets;
using Domain.Common;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.InventoryCounts;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryAdjustments;

internal sealed class InventoryAdjustmentSubmissionValidator(
    IApplicationDbContext context,
    ICapabilityCheckService capabilityCheckService)
    : IDocumentSubmissionValidator
{
    public DocumentType DocumentType => DocumentType.Adjustment;

    public async Task<Result> ValidateAsync(
        WarehouseDocument document,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        if (document.ReversalOfDocumentId is not null)
        {
            return Result.Success();
        }

        InventoryAdjustment? adjustment = await context.InventoryAdjustments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == document.Id, cancellationToken);
        if (adjustment is null)
        {
            return Result.Failure(InventoryAdjustmentErrors.Required(document.Id));
        }

        if (lines.Count == 0)
        {
            return Result.Failure(WarehouseDocumentErrors.LinesRequired(document.Id));
        }

        List<AdjustmentLine> adjustmentLines = await context.AdjustmentLines.AsNoTracking()
            .Where(item => item.AdjustmentId == document.Id)
            .ToListAsync(cancellationToken);

        Guid[] domainIds = await (
                from line in context.DocumentLines.AsNoTracking()
                join material in context.Materials.AsNoTracking() on line.MaterialId equals material.Id
                join family in context.MaterialFamilies.AsNoTracking() on material.FamilyId equals family.Id
                join category in context.MaterialCategories.AsNoTracking() on family.CategoryId equals category.Id
                where line.DocumentId == document.Id
                select category.MaterialDomainId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        foreach (Guid domainId in domainIds)
        {
            Result capability = await capabilityCheckService.EnsureAllowedAsync(
                document.WarehouseId, domainId, OperationType.Adjustment, cancellationToken);
            if (capability.IsFailure)
            {
                return capability;
            }
        }

        if (adjustment.AdjustmentKind == AdjustmentKind.Disposal)
        {
            var disposalRows = await (
                    from selection in context.DocumentLineAssetSelections.AsNoTracking()
                    join line in context.DocumentLines.AsNoTracking()
                        on new { selection.DocumentLineId, selection.DocumentId }
                        equals new { DocumentLineId = line.Id, line.DocumentId }
                    join detail in context.AdjustmentLines.AsNoTracking()
                        on new { line.Id, AdjustmentId = line.DocumentId }
                        equals new { detail.Id, detail.AdjustmentId }
                    join asset in context.Assets.AsNoTracking() on selection.AssetId equals asset.Id
                    join status in context.AssetCurrentStatuses.AsNoTracking() on asset.Id equals status.AssetId
                    where selection.DocumentId == document.Id
                    select new
                    {
                        Line = line,
                        Detail = detail,
                        Asset = asset,
                        status.CurrentStatus
                    })
                .ToListAsync(cancellationToken);
            if (disposalRows.Count != lines.Count || adjustmentLines.Count != lines.Count)
            {
                return Result.Failure(AdjustmentLineErrors.DifferenceMustMatchDocumentLine);
            }

            foreach (var row in disposalRows)
            {
                decimal expected = row.CurrentStatus == AssetCurrentStatus.InStock ? -1m : 0m;
                if (row.Line.LineType != DocumentLineType.Asset || row.Line.BaseQuantity != 1m ||
                    row.Asset.MaterialId != row.Line.MaterialId || row.Asset.WarehouseId != document.WarehouseId ||
                    row.CurrentStatus is not (AssetCurrentStatus.InStock or AssetCurrentStatus.Issued or AssetCurrentStatus.InCustody) ||
                    row.Detail.Difference != expected)
                {
                    return row.CurrentStatus == AssetCurrentStatus.Disposed
                        ? Result.Failure(DisposalErrors.AssetAlreadyDisposed(row.Asset.Id))
                        : Result.Failure(DisposalErrors.AssetStateChanged(row.Asset.Id));
                }
            }

            return Result.Success();
        }

        if (adjustmentLines.Count != lines.Count || lines.Any(line =>
                adjustmentLines.All(item => item.Id != line.Id || Math.Abs(item.Difference) != line.BaseQuantity)))
        {
            return Result.Failure(AdjustmentLineErrors.DifferenceMustMatchDocumentLine);
        }

        if (lines.Any(line => line.LineType == DocumentLineType.Asset) ||
            adjustmentLines.Any(line => line.Difference == 0 || string.IsNullOrWhiteSpace(line.Reason)))
        {
            return Result.Failure(AdjustmentLineErrors.AssetQuantityAdjustmentNotSupported);
        }

        if (adjustment.CountId is Guid countId)
        {
            InventoryCountStatus? countStatus = await context.InventoryCounts.AsNoTracking()
                .Where(item => item.Id == countId)
                .Select(item => (InventoryCountStatus?)item.Status)
                .SingleOrDefaultAsync(cancellationToken);
            if (countStatus != InventoryCountStatus.Closed)
            {
                return Result.Failure(InventoryAdjustmentErrors.CountSourceDrifted(countId));
            }

            var expected = await context.InventoryCountLines.AsNoTracking()
                .Where(item => item.CountId == countId && item.AssetId == null &&
                    item.Difference != null && item.Difference != 0)
                .Select(item => new { item.MaterialId, Difference = item.Difference!.Value, item.VarianceReason })
                .ToListAsync(cancellationToken);
            var actual = (from line in lines
                          join detail in adjustmentLines on line.Id equals detail.Id
                          select new { line.MaterialId, detail.Difference, VarianceReason = (string?)detail.Reason })
                .ToList();
            bool matches = expected.Count == actual.Count && expected.All(source => actual.Any(item =>
                item.MaterialId == source.MaterialId && item.Difference == source.Difference &&
                item.VarianceReason == source.VarianceReason));
            if (!matches)
            {
                return Result.Failure(InventoryAdjustmentErrors.CountSourceDrifted(countId));
            }
        }

        return Result.Success();
    }
}
