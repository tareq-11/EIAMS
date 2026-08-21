using Application.Abstractions.Assets;
using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.Common;
using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class OpeningPostingStrategy(
    IApplicationDbContext dbContext,
    IInventoryKeyLock inventoryKeyLock,
    IReceivedAssetFactory receivedAssetFactory) : IDocumentPostingStrategy
{
    public DocumentType DocumentType => DocumentType.Opening;

    public async Task<Result<PostingPlan>> PrepareAsync(
        DocumentPostingContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lines.Count == 0)
        {
            return Result.Failure<PostingPlan>(WarehouseDocumentErrors.LinesRequired(context.Document.Id));
        }

        DocumentLine? correctionLine = context.Lines
            .FirstOrDefault(line => line.OpeningType == OpeningType.Correction);

        if (correctionLine is not null)
        {
            return Result.Failure<PostingPlan>(OpeningDocumentErrors.CorrectionRequiresAdjustment(
                context.Document.Id,
                correctionLine.Id));
        }

        DocumentLine? missingTypeLine = context.Lines.FirstOrDefault(line => line.OpeningType is null);

        if (missingTypeLine is not null)
        {
            return Result.Failure<PostingPlan>(
                DocumentLineErrors.OpeningTypeRequired(context.Document.Id));
        }

        Guid? duplicateMaterialId = context.Lines
            .GroupBy(line => line.MaterialId)
            .Where(group => group.Count() > 1)
            .Select(group => (Guid?)group.Key)
            .FirstOrDefault();

        if (duplicateMaterialId is not null)
        {
            return Result.Failure<PostingPlan>(OpeningDocumentErrors.DuplicateMaterial(
                context.Document.Id,
                duplicateMaterialId.Value));
        }

        Result<IReadOnlyDictionary<Guid, PostingMaterialInfo>> catalogResult =
            await PostingMaterialCatalogLoader.LoadAsync(
                dbContext,
                context.Document.Id,
                context.Lines,
                cancellationToken);

        if (catalogResult.IsFailure)
        {
            return Result.Failure<PostingPlan>(catalogResult.Error);
        }

        (Guid WarehouseId, Guid MaterialId)[] keys = context.Lines
            .Select(line => (context.Document.WarehouseId, line.MaterialId))
            .ToArray();

        await inventoryKeyLock.AcquireAsync(keys, cancellationToken);

        Guid[] materialIds = context.Lines.Select(line => line.MaterialId).ToArray();
        Guid? initializedMaterialId = await dbContext.StockMovements
            .Where(movement =>
                movement.WarehouseId == context.Document.WarehouseId &&
                materialIds.Contains(movement.MaterialId))
            .Select(movement => (Guid?)movement.MaterialId)
            .FirstOrDefaultAsync(cancellationToken);

        if (initializedMaterialId is not null)
        {
            return Result.Failure<PostingPlan>(OpeningDocumentErrors.AlreadyInitialized(
                context.Document.WarehouseId,
                initializedMaterialId.Value));
        }

        var movements = context.Lines
            .Select(line => new MovementDraft(
                context.Document.WarehouseId,
                line.MaterialId,
                context.Document.Id,
                line.Id,
                MovementType.Opening,
                line.BaseQuantity))
            .ToList();

        return new PostingPlan(movements);
    }

    public Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken)
    {
        foreach (DocumentLine line in context.Lines)
        {
            Result<IReadOnlyList<Asset>> assetsResult = receivedAssetFactory.CreateForLine(
                line,
                context.Document.WarehouseId,
                context.PostedAtUtc);

            if (assetsResult.IsFailure)
            {
                return Task.FromResult(Result.Failure(assetsResult.Error));
            }

            dbContext.Assets.AddRange(assetsResult.Value);

            foreach (Asset asset in assetsResult.Value)
            {
                Result<AssetMovementHistory> historyResult = AssetMovementHistory.Create(
                    Guid.NewGuid(),
                    asset.Id,
                    context.Document.Id,
                    AssetMovementType.Received,
                    context.PostedAtUtc);

                if (historyResult.IsFailure)
                {
                    return Task.FromResult(Result.Failure(historyResult.Error));
                }

                dbContext.AssetMovementHistories.Add(historyResult.Value);
            }
        }

        return Task.FromResult(Result.Success());
    }
}
