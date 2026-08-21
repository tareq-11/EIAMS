using Application.Abstractions.Assets;
using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Application.Abstractions.Warehouses;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DocumentLineAssetSelections;
using Domain.InventoryAdjustments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class AdjustmentPostingStrategy(
    IApplicationDbContext dbContext,
    ICapabilityCheckService capabilityCheckService,
    IAssetKeyLock assetKeyLock) : IDocumentPostingStrategy
{
    private readonly Dictionary<Guid, DisposalPreparation> disposalPreparations = [];
    public DocumentType DocumentType => DocumentType.Adjustment;

    public async Task<Result<PostingPlan>> PrepareAsync(DocumentPostingContext context, CancellationToken cancellationToken)
    {
        InventoryAdjustment? adjustment = await dbContext.InventoryAdjustments
            .SingleOrDefaultAsync(item => item.Id == context.Document.Id, cancellationToken);
        if (adjustment is null)
        {
            return Result.Failure<PostingPlan>(InventoryAdjustmentErrors.Required(context.Document.Id));
        }

        Result<IReadOnlyDictionary<Guid, PostingMaterialInfo>> catalogResult =
            await PostingMaterialCatalogLoader.LoadAsync(dbContext, context.Document.Id, context.Lines, cancellationToken);
        if (catalogResult.IsFailure)
        {
            return Result.Failure<PostingPlan>(catalogResult.Error);
        }

        foreach (Guid domainId in catalogResult.Value.Values.Select(item => item.MaterialDomainId).Distinct())
        {
            Result capability = await capabilityCheckService.EnsureAllowedAsync(
                context.Document.WarehouseId, domainId, OperationType.Adjustment, cancellationToken);
            if (capability.IsFailure)
            {
                return Result.Failure<PostingPlan>(capability.Error);
            }
        }

        return adjustment.AdjustmentKind == AdjustmentKind.Disposal
            ? await PrepareDisposalAsync(context, cancellationToken)
            : await PrepareQuantityAsync(context, cancellationToken);
    }

    public async Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken)
    {
        InventoryAdjustment? adjustment = await dbContext.InventoryAdjustments
            .SingleOrDefaultAsync(item => item.Id == context.Document.Id, cancellationToken);
        if (adjustment is null)
        {
            return Result.Failure(InventoryAdjustmentErrors.Required(context.Document.Id));
        }

        if (adjustment.AdjustmentKind == AdjustmentKind.Disposal)
        {
            DisposalPreparation prepared = disposalPreparations[context.Document.Id];
            foreach (DisposalAsset item in prepared.Assets)
            {
                Result<AssetMovementHistory> history = AssetMovementHistory.Create(
                    Guid.NewGuid(), item.AssetId, context.Document.Id,
                    AssetMovementType.Disposed, context.PostedAtUtc);
                if (history.IsFailure)
                {
                    return Result.Failure(history.Error);
                }

                dbContext.AssetMovementHistories.Add(history.Value);
                if (item.Custody is not null)
                {
                    Result close = item.Custody.CloseForDisposal(context.Document.Id, context.PostedAtUtc);
                    if (close.IsFailure)
                    {
                        return close;
                    }

                    Result<CustodyHistory> custodyHistory = CustodyHistory.Create(
                        Guid.NewGuid(), item.Custody.Id, CustodyStatus.Active, CustodyStatus.Closed,
                        context.PostedBy, context.PostedAtUtc, "Asset disposed");
                    if (custodyHistory.IsFailure)
                    {
                        return Result.Failure(custodyHistory.Error);
                    }

                    dbContext.CustodyHistories.Add(custodyHistory.Value);
                }
            }
        }

        return adjustment.MarkPosted();
    }

    private async Task<Result<PostingPlan>> PrepareQuantityAsync(
        DocumentPostingContext context,
        CancellationToken cancellationToken)
    {
        List<AdjustmentLine> adjustmentLines = await dbContext.AdjustmentLines.AsNoTracking()
            .Where(item => item.AdjustmentId == context.Document.Id).ToListAsync(cancellationToken);
        if (adjustmentLines.Count != context.Lines.Count)
        {
            return Result.Failure<PostingPlan>(AdjustmentLineErrors.DifferenceMustMatchDocumentLine);
        }

        var differences = adjustmentLines.ToDictionary(item => item.Id, item => item.Difference);
        return new PostingPlan(context.Lines.Select(line => new MovementDraft(
            context.Document.WarehouseId, line.MaterialId, context.Document.Id, line.Id,
            differences[line.Id] > 0 ? MovementType.AdjustmentIn : MovementType.AdjustmentOut,
            differences[line.Id])).ToList());
    }

    private async Task<Result<PostingPlan>> PrepareDisposalAsync(
        DocumentPostingContext context,
        CancellationToken cancellationToken)
    {
        List<DocumentLineAssetSelection> selections = await dbContext.DocumentLineAssetSelections.AsNoTracking()
            .Where(item => item.DocumentId == context.Document.Id).ToListAsync(cancellationToken);
        if (selections.Count != context.Lines.Count || selections.Count == 0)
        {
            return Result.Failure<PostingPlan>(AdjustmentLineErrors.DifferenceMustMatchDocumentLine);
        }

        await assetKeyLock.AcquireAsync(selections.Select(item => item.AssetId), cancellationToken);
        Guid[] assetIds = selections.Select(item => item.AssetId).ToArray();
        List<AssetCurrentStatusView> statuses = await dbContext.AssetCurrentStatuses.AsNoTracking()
            .Where(item => assetIds.Contains(item.AssetId)).ToListAsync(cancellationToken);
        List<Custody> custodies = await dbContext.Custodies
            .Where(item => assetIds.Contains(item.AssetId) && item.Status == CustodyStatus.Active)
            .ToListAsync(cancellationToken);
        List<AdjustmentLine> adjustmentLines = await dbContext.AdjustmentLines.AsNoTracking()
            .Where(item => item.AdjustmentId == context.Document.Id).ToListAsync(cancellationToken);

        var statusByAsset = statuses.ToDictionary(item => item.AssetId);
        var lineById = context.Lines.ToDictionary(item => item.Id);
        var adjustmentByLine = adjustmentLines.ToDictionary(item => item.Id);
        var prepared = new List<DisposalAsset>();
        var movements = new List<MovementDraft>();

        foreach (DocumentLineAssetSelection selection in selections)
        {
            if (!statusByAsset.TryGetValue(selection.AssetId, out AssetCurrentStatusView? status) ||
                status.CurrentStatus == AssetCurrentStatus.Disposed)
            {
                return Result.Failure<PostingPlan>(DisposalErrors.AssetAlreadyDisposed(selection.AssetId));
            }

            bool inStock = status.CurrentStatus == AssetCurrentStatus.InStock;
            Custody? custody = custodies.SingleOrDefault(item => item.AssetId == selection.AssetId);
            if (!inStock && custody is null)
            {
                return Result.Failure<PostingPlan>(DisposalErrors.AssetStateChanged(selection.AssetId));
            }

            decimal expectedDifference = inStock ? -1m : 0m;
            if (!adjustmentByLine.TryGetValue(selection.DocumentLineId, out AdjustmentLine? adjustmentLine) ||
                adjustmentLine.Difference != expectedDifference)
            {
                return Result.Failure<PostingPlan>(DisposalErrors.AssetStateChanged(selection.AssetId));
            }

            prepared.Add(new DisposalAsset(selection.AssetId, custody));
            if (inStock)
            {
                Domain.DocumentLines.DocumentLine line = lineById[selection.DocumentLineId];
                movements.Add(new MovementDraft(context.Document.WarehouseId, line.MaterialId,
                    context.Document.Id, line.Id, MovementType.AdjustmentOut, -1m));
            }
        }

        disposalPreparations[context.Document.Id] = new DisposalPreparation(prepared);
        return new PostingPlan(movements);
    }

    private sealed record DisposalPreparation(IReadOnlyList<DisposalAsset> Assets);
    private sealed record DisposalAsset(Guid AssetId, Custody? Custody);
}
