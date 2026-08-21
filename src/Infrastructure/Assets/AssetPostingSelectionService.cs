using Application.Abstractions.Assets;
using Application.Abstractions.Data;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.ReturnInfos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Assets;

/// <summary>
/// Re-validates persisted asset selections while their asset identifiers are transaction-locked.
/// Submission catches incomplete drafts; this service protects posting from races after Submit.
/// </summary>
internal sealed class AssetPostingSelectionService(
    IApplicationDbContext context,
    IAssetKeyLock assetKeyLock,
    IAssetLifecycleGuard assetLifecycleGuard)
{
    private readonly Dictionary<Guid, IReadOnlyList<Asset>> issueSelectionsByDocumentId = [];
    private readonly Dictionary<Guid, IReadOnlyList<AssetCustodySelection>> returnSelectionsByDocumentId = [];

    public async Task<Result<IReadOnlyList<Asset>>> LockAndValidateForIssueAsync(
        WarehouseDocument document,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<DocumentLineAssetSelection>> selectionsResult =
            await LoadAndValidateCountsAsync(document.Id, lines, cancellationToken);

        if (selectionsResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<Asset>>(selectionsResult.Error);
        }

        IReadOnlyList<DocumentLineAssetSelection> selections = selectionsResult.Value;

        if (selections.Count == 0)
        {
            IReadOnlyList<Asset> noAssets = Array.Empty<Asset>();
            issueSelectionsByDocumentId[document.Id] = noAssets;
            return Result.Success(noAssets);
        }

        await assetKeyLock.AcquireAsync(selections.Select(selection => selection.AssetId), cancellationToken);

        Result terminalResult = await assetLifecycleGuard.EnsureNotDisposedAsync(
            selections.Select(selection => selection.AssetId), cancellationToken);
        if (terminalResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<Asset>>(terminalResult.Error);
        }

        List<Asset> assets = await LoadAssetsAsync(selections, cancellationToken);

        if (assets.Count != selections.Count)
        {
            Guid missingAssetId = selections.Select(selection => selection.AssetId)
                .Except(assets.Select(asset => asset.Id))
                .First();
            return Result.Failure<IReadOnlyList<Asset>>(AssetErrors.NotFound(missingAssetId));
        }

        var lineById = lines.ToDictionary(line => line.Id);
        var assetById = assets.ToDictionary(asset => asset.Id);

        foreach (DocumentLineAssetSelection selection in selections)
        {
            Asset asset = assetById[selection.AssetId];
            DocumentLine line = lineById[selection.DocumentLineId];

            if (asset.MaterialId != line.MaterialId)
            {
                return Result.Failure<IReadOnlyList<Asset>>(
                    DocumentLineAssetSelectionErrors.AssetNotForLineMaterial(asset.Id, line.Id));
            }

            if (asset.WarehouseId != document.WarehouseId)
            {
                return Result.Failure<IReadOnlyList<Asset>>(
                    DocumentLineAssetSelectionErrors.AssetNotInSourceWarehouse(asset.Id, document.WarehouseId));
            }
        }

        Guid[] assetIds = assets.Select(asset => asset.Id).ToArray();
        bool hasActiveCustody = await context.Custodies
            .AnyAsync(custody => assetIds.Contains(custody.AssetId) && custody.Status == CustodyStatus.Active,
                cancellationToken);

        if (hasActiveCustody)
        {
            Guid assetId = await context.Custodies
                .Where(custody => assetIds.Contains(custody.AssetId) && custody.Status == CustodyStatus.Active)
                .Select(custody => custody.AssetId)
                .FirstAsync(cancellationToken);
            return Result.Failure<IReadOnlyList<Asset>>(DocumentLineAssetSelectionErrors.AssetNotInStock(assetId));
        }

        List<AssetMovementHistory> histories = await context.AssetMovementHistories
            .AsNoTracking()
            .Where(history => assetIds.Contains(history.AssetId))
            .ToListAsync(cancellationToken);

        foreach (Asset asset in assets)
        {
            AssetMovementHistory? latestHistory = histories
                .Where(history => history.AssetId == asset.Id)
                .OrderByDescending(history => history.MovedAtUtc)
                .ThenByDescending(history => history.Id)
                .FirstOrDefault();

            if (latestHistory?.MovementType is not (AssetMovementType.Received or AssetMovementType.Returned))
            {
                return Result.Failure<IReadOnlyList<Asset>>(
                    DocumentLineAssetSelectionErrors.AssetNotInStock(asset.Id));
            }
        }

        issueSelectionsByDocumentId[document.Id] = assets;
        return Result.Success<IReadOnlyList<Asset>>(assets);
    }

    public async Task<Result<IReadOnlyList<AssetCustodySelection>>> LockAndValidateForReturnAsync(
        WarehouseDocument document,
        ReturnInfo returnInfo,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<DocumentLineAssetSelection>> selectionsResult =
            await LoadAndValidateCountsAsync(document.Id, lines, cancellationToken);

        if (selectionsResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AssetCustodySelection>>(selectionsResult.Error);
        }

        IReadOnlyList<DocumentLineAssetSelection> selections = selectionsResult.Value;

        if (selections.Count == 0)
        {
            IReadOnlyList<AssetCustodySelection> noAssets = Array.Empty<AssetCustodySelection>();
            returnSelectionsByDocumentId[document.Id] = noAssets;
            return Result.Success(noAssets);
        }

        await assetKeyLock.AcquireAsync(selections.Select(selection => selection.AssetId), cancellationToken);

        Result terminalResult = await assetLifecycleGuard.EnsureNotDisposedAsync(
            selections.Select(selection => selection.AssetId), cancellationToken);
        if (terminalResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AssetCustodySelection>>(terminalResult.Error);
        }

        List<Asset> assets = await LoadAssetsAsync(selections, cancellationToken);

        if (assets.Count != selections.Count)
        {
            Guid missingAssetId = selections.Select(selection => selection.AssetId)
                .Except(assets.Select(asset => asset.Id))
                .First();
            return Result.Failure<IReadOnlyList<AssetCustodySelection>>(AssetErrors.NotFound(missingAssetId));
        }

        var lineById = lines.ToDictionary(line => line.Id);
        var assetById = assets.ToDictionary(asset => asset.Id);
        Guid[] assetIds = assets.Select(asset => asset.Id).ToArray();
        List<Custody> activeCustodies = await context.Custodies
            .Where(custody =>
                assetIds.Contains(custody.AssetId) &&
                custody.Status == CustodyStatus.Active &&
                custody.IssueDocumentId == returnInfo.OriginalIssueDocumentId)
            .ToListAsync(cancellationToken);

        foreach (DocumentLineAssetSelection selection in selections)
        {
            Asset asset = assetById[selection.AssetId];
            DocumentLine line = lineById[selection.DocumentLineId];

            if (asset.MaterialId != line.MaterialId)
            {
                return Result.Failure<IReadOnlyList<AssetCustodySelection>>(
                    DocumentLineAssetSelectionErrors.AssetNotForLineMaterial(asset.Id, line.Id));
            }

            if (asset.WarehouseId != document.WarehouseId)
            {
                return Result.Failure<IReadOnlyList<AssetCustodySelection>>(
                    DocumentLineAssetSelectionErrors.AssetNotInSourceWarehouse(asset.Id, document.WarehouseId));
            }
        }

        if (activeCustodies.Count != assets.Count)
        {
            Guid assetId = assetIds.Except(activeCustodies.Select(custody => custody.AssetId)).First();
            return Result.Failure<IReadOnlyList<AssetCustodySelection>>(
                DocumentLineAssetSelectionErrors.ActiveCustodyMismatch(
                    assetId,
                    returnInfo.OriginalIssueDocumentId));
        }

        var custodyByAssetId = activeCustodies.ToDictionary(custody => custody.AssetId);

        IReadOnlyList<AssetCustodySelection> result = assets
            .Select(asset => new AssetCustodySelection(asset, custodyByAssetId[asset.Id]))
            .ToList();
        returnSelectionsByDocumentId[document.Id] = result;
        return Result.Success(result);
    }

    /// <summary>Returns the Issue selections previously locked and validated during Prepare.</summary>
    public IReadOnlyList<Asset> GetPreparedIssueSelections(Guid documentId) =>
        issueSelectionsByDocumentId.TryGetValue(documentId, out IReadOnlyList<Asset>? selections)
            ? selections
            : throw new InvalidOperationException(
                "Issue asset selections must be prepared before applying posting side effects.");

    /// <summary>Returns the Return selections previously locked and validated during Prepare.</summary>
    public IReadOnlyList<AssetCustodySelection> GetPreparedReturnSelections(Guid documentId) =>
        returnSelectionsByDocumentId.TryGetValue(documentId, out IReadOnlyList<AssetCustodySelection>? selections)
            ? selections
            : throw new InvalidOperationException(
                "Return asset selections must be prepared before applying posting side effects.");

    private async Task<Result<IReadOnlyList<DocumentLineAssetSelection>>> LoadAndValidateCountsAsync(
        Guid documentId,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        List<DocumentLineAssetSelection> selections = await context.DocumentLineAssetSelections
            .AsNoTracking()
            .Where(selection => selection.DocumentId == documentId)
            .ToListAsync(cancellationToken);
        var lineById = lines.ToDictionary(line => line.Id);

        foreach (DocumentLineAssetSelection selection in selections)
        {
            if (!lineById.TryGetValue(selection.DocumentLineId, out DocumentLine? line) ||
                line.LineType != DocumentLineType.Asset)
            {
                return Result.Failure<IReadOnlyList<DocumentLineAssetSelection>>(
                    DocumentLineAssetSelectionErrors.UnsupportedLineType(selection.DocumentLineId));
            }
        }

        foreach (DocumentLine assetLine in lines.Where(line => line.LineType == DocumentLineType.Asset))
        {
            int selectedCount = selections.Count(selection => selection.DocumentLineId == assetLine.Id);

            if (decimal.Truncate(assetLine.BaseQuantity) != assetLine.BaseQuantity ||
                selectedCount != decimal.ToInt32(assetLine.BaseQuantity))
            {
                return Result.Failure<IReadOnlyList<DocumentLineAssetSelection>>(
                    DocumentLineAssetSelectionErrors.CountMismatch(
                        assetLine.Id,
                        assetLine.BaseQuantity,
                        selectedCount));
            }
        }

        return selections;
    }

    private async Task<List<Asset>> LoadAssetsAsync(
        IReadOnlyList<DocumentLineAssetSelection> selections,
        CancellationToken cancellationToken)
    {
        Guid[] assetIds = selections.Select(selection => selection.AssetId).ToArray();

        return await context.Assets
            .Where(asset => assetIds.Contains(asset.Id))
            .OrderBy(asset => asset.Id)
            .ToListAsync(cancellationToken);
    }
}

internal sealed record AssetCustodySelection(Asset Asset, Custody Custody);
