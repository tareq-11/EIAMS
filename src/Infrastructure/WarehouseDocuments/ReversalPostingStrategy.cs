using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Domain.DocumentLines;
using Domain.StockMovements;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

/// <summary>
/// Negates a source document's movements and marks the source Reversed. Every copied reversal line
/// carries an immutable SourceLineId, so movement linkage never depends on timestamps or random
/// identifier ordering.
/// </summary>
internal sealed class ReversalPostingStrategy(
    IApplicationDbContext dbContext,
    IEnumerable<IDocumentReversalSideEffectStrategy> sideEffectStrategies) : IReversalPostingStrategy
{
    public async Task<Result<PostingPlan>> PrepareAsync(
        DocumentPostingContext context,
        CancellationToken cancellationToken)
    {
        Guid sourceDocumentId = context.Document.ReversalOfDocumentId!.Value;

        List<DocumentLine> sourceLines = await dbContext.DocumentLines
            .Where(l => l.DocumentId == sourceDocumentId)
            .OrderBy(l => l.CreatedAtUtc)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        if (sourceLines.Count != context.Lines.Count ||
            context.Lines.Any(line => line.SourceLineId is null) ||
            context.Lines.Select(line => line.SourceLineId).Distinct().Count() != context.Lines.Count)
        {
            return Result.Failure<PostingPlan>(WarehouseDocumentErrors.ReversalLineMismatch(context.Document.Id));
        }

        var reversalLineBySourceLineId = context.Lines
            .ToDictionary(line => line.SourceLineId!.Value);

        foreach (DocumentLine sourceLine in sourceLines)
        {
            if (!reversalLineBySourceLineId.TryGetValue(sourceLine.Id, out DocumentLine? reversalLine) ||
                !IsExactCopy(sourceLine, reversalLine))
            {
                return Result.Failure<PostingPlan>(
                    WarehouseDocumentErrors.ReversalLineMismatch(context.Document.Id));
            }
        }

        List<StockMovement> sourceMovements = await dbContext.StockMovements
            .Where(m => m.DocumentId == sourceDocumentId)
            .ToListAsync(cancellationToken);

        var movements = sourceMovements
            .Select(m => new MovementDraft(
                m.WarehouseId,
                m.MaterialId,
                context.Document.Id,
                reversalLineBySourceLineId[m.LineId].Id,
                m.MovementType,
                -m.QuantityDelta))
            .ToList();

        return new PostingPlan(movements);
    }

    private static bool IsExactCopy(DocumentLine source, DocumentLine reversal) =>
        source.MaterialId == reversal.MaterialId &&
        source.LineType == reversal.LineType &&
        source.Quantity == reversal.Quantity &&
        source.UnitId == reversal.UnitId &&
        source.BaseQuantity == reversal.BaseQuantity &&
        source.UnitPrice == reversal.UnitPrice &&
        source.BatchNumber == reversal.BatchNumber &&
        source.ExpiryDate == reversal.ExpiryDate &&
        source.OpeningType == reversal.OpeningType;

    public async Task<Result> ValidateSideEffectsAsync(
        DocumentPostingContext context,
        CancellationToken cancellationToken)
    {
        Result<ReversalSideEffectContext> sideEffectContextResult =
            await ResolveSideEffectContextAsync(context, cancellationToken);

        if (sideEffectContextResult.IsFailure)
        {
            return Result.Failure(sideEffectContextResult.Error);
        }

        ReversalSideEffectContext sideEffectContext = sideEffectContextResult.Value;

        return sideEffectContext.Strategy is null
            ? Result.Success()
            : await sideEffectContext.Strategy.ValidateAsync(
                sideEffectContext.SourceDocument,
                context.Document,
                cancellationToken);
    }

    public async Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken)
    {
        Result<ReversalSideEffectContext> sideEffectContextResult =
            await ResolveSideEffectContextAsync(context, cancellationToken);

        if (sideEffectContextResult.IsFailure)
        {
            return Result.Failure(sideEffectContextResult.Error);
        }

        ReversalSideEffectContext sideEffectContext = sideEffectContextResult.Value;

        if (sideEffectContext.Strategy is not null)
        {
            Result sideEffectResult = await sideEffectContext.Strategy.ApplyAsync(
                sideEffectContext.SourceDocument,
                context.Document,
                context.PostedBy,
                context.PostedAtUtc,
                cancellationToken);

            if (sideEffectResult.IsFailure)
            {
                return sideEffectResult;
            }
        }

        return sideEffectContext.SourceDocument.MarkReversed();
    }

    private async Task<Result<ReversalSideEffectContext>> ResolveSideEffectContextAsync(
        DocumentPostingContext context,
        CancellationToken cancellationToken)
    {
        Guid sourceDocumentId = context.Document.ReversalOfDocumentId!.Value;
        WarehouseDocument? sourceDocument = await dbContext.WarehouseDocuments
            .SingleOrDefaultAsync(document => document.Id == sourceDocumentId, cancellationToken);

        if (sourceDocument is null)
        {
            return Result.Failure<ReversalSideEffectContext>(
                WarehouseDocumentErrors.NotFound(sourceDocumentId));
        }

        IDocumentReversalSideEffectStrategy? strategy = sideEffectStrategies
            .SingleOrDefault(candidate => candidate.DocumentTypes.Contains(sourceDocument.DocumentType));

        return new ReversalSideEffectContext(sourceDocument, strategy);
    }

    private sealed record ReversalSideEffectContext(
        WarehouseDocument SourceDocument,
        IDocumentReversalSideEffectStrategy? Strategy);
}
