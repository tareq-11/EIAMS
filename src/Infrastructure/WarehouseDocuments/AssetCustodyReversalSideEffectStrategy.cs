using Application.Abstractions.Assets;
using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Domain.AssetMovementHistories;
using Domain.Common;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DocumentLineAssetSelections;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

/// <summary>
/// Compensates the custody/history effects of asset Issue and Return documents while the generic
/// reversal strategy compensates their stock movements.
/// </summary>
internal sealed class AssetCustodyReversalSideEffectStrategy(
    IApplicationDbContext context,
    IAssetKeyLock assetKeyLock,
    IAssetLifecycleGuard assetLifecycleGuard)
    : IDocumentReversalSideEffectStrategy
{
    private static readonly DocumentType[] SupportedTypes = [DocumentType.Issue, DocumentType.Return];

    public IReadOnlyCollection<DocumentType> DocumentTypes => SupportedTypes;

    public async Task<Result> ValidateAsync(
        WarehouseDocument source,
        WarehouseDocument reversal,
        CancellationToken cancellationToken)
    {
        Guid[] assetIds = await LoadSelectedAssetIdsAsync(source.Id, cancellationToken);

        if (assetIds.Length == 0)
        {
            return Result.Success();
        }

        await assetKeyLock.AcquireAsync(assetIds, cancellationToken);

        Result terminalResult = await assetLifecycleGuard.EnsureNotDisposedAsync(assetIds, cancellationToken);
        if (terminalResult.IsFailure)
        {
            return terminalResult;
        }

        return source.DocumentType switch
        {
            DocumentType.Issue => await ValidateIssueReversalAsync(source, assetIds, cancellationToken),
            DocumentType.Return => await ValidateReturnReversalAsync(source, assetIds, cancellationToken),
            _ => Result.Success()
        };
    }

    public async Task<Result> ApplyAsync(
        WarehouseDocument source,
        WarehouseDocument reversal,
        Guid postedBy,
        DateTime postedAtUtc,
        CancellationToken cancellationToken)
    {
        Guid[] assetIds = await LoadSelectedAssetIdsAsync(source.Id, cancellationToken);

        if (assetIds.Length == 0)
        {
            return Result.Success();
        }

        await assetKeyLock.AcquireAsync(assetIds, cancellationToken);

        Result terminalResult = await assetLifecycleGuard.EnsureNotDisposedAsync(assetIds, cancellationToken);
        if (terminalResult.IsFailure)
        {
            return terminalResult;
        }

        return source.DocumentType switch
        {
            DocumentType.Issue => await ReverseIssueAsync(source, reversal, postedBy, postedAtUtc, assetIds, cancellationToken),
            DocumentType.Return => await ReverseReturnAsync(source, reversal, postedBy, postedAtUtc, assetIds, cancellationToken),
            _ => Result.Success()
        };
    }

    private async Task<Result> ValidateIssueReversalAsync(
        WarehouseDocument source,
        Guid[] assetIds,
        CancellationToken cancellationToken)
    {
        List<Custody> custodies = await context.Custodies
            .AsNoTracking()
            .Where(custody =>
                assetIds.Contains(custody.AssetId) &&
                custody.IssueDocumentId == source.Id)
            .ToListAsync(cancellationToken);

        foreach (Guid assetId in assetIds)
        {
            var assetCustodies = custodies
                .Where(custody => custody.AssetId == assetId)
                .ToList();

            if (assetCustodies.Count == 0 || assetCustodies.All(custody => custody.Status != CustodyStatus.Active))
            {
                return Result.Failure(CustodyErrors.NoActiveCustody(assetId));
            }

            Custody activeCustody = assetCustodies.Single(custody => custody.Status == CustodyStatus.Active);

            if (assetCustodies.Count != 1 || activeCustody.CustodyKind != CustodyKind.Operational)
            {
                return Result.Failure(CustodyErrors.CannotReverseChangedCustody(activeCustody.Id));
            }
        }

        return await AllLatestHistoriesMatchAsync(
            assetIds,
            source.Id,
            AssetMovementType.Issued,
            cancellationToken)
            ? Result.Success()
            : Result.Failure(CustodyErrors.CannotReverseChangedCustody(
                custodies.Single(custody => custody.AssetId == assetIds[0]).Id));
    }

    private async Task<Result> ValidateReturnReversalAsync(
        WarehouseDocument source,
        Guid[] assetIds,
        CancellationToken cancellationToken)
    {
        List<Custody> custodies = await context.Custodies
            .AsNoTracking()
            .Where(custody =>
                assetIds.Contains(custody.AssetId) &&
                custody.ReturnDocumentId == source.Id &&
                custody.Status == CustodyStatus.Closed)
            .ToListAsync(cancellationToken);

        if (custodies.Count != assetIds.Length)
        {
            return Result.Failure(CustodyErrors.CannotReverseChangedCustody(custodies.FirstOrDefault()?.Id ?? Guid.Empty));
        }

        bool hasNewActiveCustody = await context.Custodies
            .AnyAsync(custody => assetIds.Contains(custody.AssetId) && custody.Status == CustodyStatus.Active,
                cancellationToken);

        if (hasNewActiveCustody || !await AllLatestHistoriesMatchAsync(
                assetIds,
                source.Id,
                AssetMovementType.Returned,
                cancellationToken))
        {
            return Result.Failure(CustodyErrors.CannotReverseChangedCustody(custodies[0].Id));
        }

        return Result.Success();
    }

    private async Task<Result> ReverseIssueAsync(
        WarehouseDocument source,
        WarehouseDocument reversal,
        Guid postedBy,
        DateTime postedAtUtc,
        Guid[] assetIds,
        CancellationToken cancellationToken)
    {
        List<Custody> custodies = await context.Custodies
            .Where(custody =>
                assetIds.Contains(custody.AssetId) &&
                custody.IssueDocumentId == source.Id &&
                custody.Status == CustodyStatus.Active &&
                custody.CustodyKind == CustodyKind.Operational)
            .OrderBy(custody => custody.AssetId)
            .ToListAsync(cancellationToken);

        if (custodies.Count != assetIds.Length)
        {
            return Result.Failure(CustodyErrors.NoActiveCustody(assetIds.Except(custodies.Select(custody => custody.AssetId)).First()));
        }

        foreach (Custody custody in custodies)
        {
            Result closeResult = custody.Close(reversal.Id, postedAtUtc);

            if (closeResult.IsFailure)
            {
                return closeResult;
            }

            Result appendResult = AppendTransitionHistories(
                custody,
                reversal,
                postedBy,
                postedAtUtc,
                AssetMovementType.Returned,
                CustodyStatus.Active,
                CustodyStatus.Closed);

            if (appendResult.IsFailure)
            {
                return appendResult;
            }
        }

        return Result.Success();
    }

    private async Task<Result> ReverseReturnAsync(
        WarehouseDocument source,
        WarehouseDocument reversal,
        Guid postedBy,
        DateTime postedAtUtc,
        Guid[] assetIds,
        CancellationToken cancellationToken)
    {
        List<Custody> custodies = await context.Custodies
            .Where(custody =>
                assetIds.Contains(custody.AssetId) &&
                custody.ReturnDocumentId == source.Id &&
                custody.Status == CustodyStatus.Closed)
            .OrderBy(custody => custody.AssetId)
            .ToListAsync(cancellationToken);

        if (custodies.Count != assetIds.Length)
        {
            return Result.Failure(CustodyErrors.CannotReverseChangedCustody(custodies.FirstOrDefault()?.Id ?? Guid.Empty));
        }

        foreach (Custody custody in custodies)
        {
            Result reopenResult = custody.Reopen();

            if (reopenResult.IsFailure)
            {
                return reopenResult;
            }

            Result appendResult = AppendTransitionHistories(
                custody,
                reversal,
                postedBy,
                postedAtUtc,
                AssetMovementType.Issued,
                CustodyStatus.Closed,
                CustodyStatus.Active);

            if (appendResult.IsFailure)
            {
                return appendResult;
            }
        }

        return Result.Success();
    }

    private Result AppendTransitionHistories(
        Custody custody,
        WarehouseDocument reversal,
        Guid postedBy,
        DateTime postedAtUtc,
        AssetMovementType movementType,
        CustodyStatus fromStatus,
        CustodyStatus toStatus)
    {
        Result<AssetMovementHistory> movementHistoryResult = AssetMovementHistory.Create(
            Guid.NewGuid(), custody.AssetId, reversal.Id, movementType, postedAtUtc);

        if (movementHistoryResult.IsFailure)
        {
            return Result.Failure(movementHistoryResult.Error);
        }

        Result<CustodyHistory> custodyHistoryResult = CustodyHistory.Create(
            Guid.NewGuid(), custody.Id, fromStatus, toStatus, postedBy, postedAtUtc, null);

        if (custodyHistoryResult.IsFailure)
        {
            return Result.Failure(custodyHistoryResult.Error);
        }

        context.AssetMovementHistories.Add(movementHistoryResult.Value);
        context.CustodyHistories.Add(custodyHistoryResult.Value);

        return Result.Success();
    }

    private async Task<Guid[]> LoadSelectedAssetIdsAsync(Guid documentId, CancellationToken cancellationToken) =>
        await context.DocumentLineAssetSelections
            .AsNoTracking()
            .Where(selection => selection.DocumentId == documentId)
            .Select(selection => selection.AssetId)
            .OrderBy(assetId => assetId)
            .ToArrayAsync(cancellationToken);

    private async Task<bool> AllLatestHistoriesMatchAsync(
        Guid[] assetIds,
        Guid documentId,
        AssetMovementType movementType,
        CancellationToken cancellationToken)
    {
        List<AssetMovementHistory> histories = await context.AssetMovementHistories
            .AsNoTracking()
            .Where(history => assetIds.Contains(history.AssetId))
            .ToListAsync(cancellationToken);

        return assetIds.All(assetId =>
        {
            AssetMovementHistory? latest = histories
                .Where(history => history.AssetId == assetId)
                .OrderByDescending(history => history.MovedAtUtc)
                .ThenByDescending(history => history.Id)
                .FirstOrDefault();

            return latest?.DocumentId == documentId && latest.MovementType == movementType;
        });
    }
}
