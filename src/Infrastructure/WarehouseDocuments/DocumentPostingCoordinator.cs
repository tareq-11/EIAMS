using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class DocumentPostingCoordinator(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IDocumentLock documentLock,
    IInventoryLedgerWriter ledgerWriter,
    IEnumerable<IDocumentPostingStrategy> strategies,
    IReversalPostingStrategy reversalPostingStrategy,
    IDateTimeProvider dateTimeProvider) : IDocumentPostingCoordinator
{
    public Task<Result<Guid>> PostAsync(
        Guid documentId,
        int expectedRowVersion,
        Guid postedBy,
        CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            ct => PostInTransactionAsync(documentId, expectedRowVersion, postedBy, ct),
            cancellationToken);

    private async Task<Result<Guid>> PostInTransactionAsync(
        Guid documentId,
        int expectedRowVersion,
        Guid postedBy,
        CancellationToken cancellationToken)
    {
        Result<WarehouseDocument> lockResult = await documentLock.LockAsync(documentId, cancellationToken);

        if (lockResult.IsFailure)
        {
            return Result.Failure<Guid>(lockResult.Error);
        }

        WarehouseDocument document = lockResult.Value;

        if (document.RowVersion != expectedRowVersion)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.RowVersionMismatch(
                documentId,
                expectedRowVersion,
                document.RowVersion));
        }

        Result postingGateResult = document.ValidateForPosting();

        if (postingGateResult.IsFailure)
        {
            return Result.Failure<Guid>(postingGateResult.Error);
        }

        bool hasValidSignedOriginal = await context.DocumentAttachments.AnyAsync(
            a => a.Id == document.SignedCopyAttachmentId &&
                 a.DocumentId == document.Id &&
                 a.AttachmentType == AttachmentType.SignedOriginal,
            cancellationToken);

        if (!hasValidSignedOriginal)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.SignedCopyRequired(document.Id));
        }

        Warehouse? warehouse = await context.Warehouses
            .SingleOrDefaultAsync(w => w.Id == document.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Result.Failure<Guid>(WarehouseErrors.NotFound(document.WarehouseId));
        }

        if (warehouse.Status != Status.Active)
        {
            return Result.Failure<Guid>(WarehouseErrors.Inactive(document.WarehouseId));
        }

        if (!warehouse.CanHoldStock)
        {
            return Result.Failure<Guid>(WarehouseErrors.CannotHoldStock(document.WarehouseId));
        }

        List<DocumentLine> lines = await context.DocumentLines
            .Where(l => l.DocumentId == documentId)
            .OrderBy(l => l.CreatedAtUtc)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        DateTime postedAtUtc = dateTimeProvider.UtcNow;
        var postingContext = new DocumentPostingContext(document, warehouse, lines, postedBy, postedAtUtc);

        bool isReversal = document.ReversalOfDocumentId is not null;

        Result<PostingPlan> planResult;

        if (isReversal)
        {
            planResult = await reversalPostingStrategy.PrepareAsync(postingContext, cancellationToken);
        }
        else
        {
            IDocumentPostingStrategy? strategy = strategies
                .SingleOrDefault(s => s.DocumentType == document.DocumentType);

            if (strategy is null)
            {
                return Result.Failure<Guid>(
                    WarehouseDocumentErrors.PostingStrategyNotAvailable(documentId, document.DocumentType));
            }

            planResult = await strategy.PrepareAsync(postingContext, cancellationToken);
        }

        if (planResult.IsFailure)
        {
            return Result.Failure<Guid>(planResult.Error);
        }

        PostingPlan plan = planResult.Value;

        Result ledgerResult = await ledgerWriter.AppendAsync(plan.Movements, postedBy, postedAtUtc, cancellationToken);

        if (ledgerResult.IsFailure)
        {
            return Result.Failure<Guid>(ledgerResult.Error);
        }

        Result sideEffectsResult = isReversal
            ? await reversalPostingStrategy.ApplySideEffectsAsync(postingContext, plan, cancellationToken)
            : await strategies
                .Single(s => s.DocumentType == document.DocumentType)
                .ApplySideEffectsAsync(postingContext, plan, cancellationToken);

        if (sideEffectsResult.IsFailure)
        {
            return Result.Failure<Guid>(sideEffectsResult.Error);
        }

        Result markPostedResult = document.MarkPosted(postedBy, postedAtUtc);

        if (markPostedResult.IsFailure)
        {
            return Result.Failure<Guid>(markPostedResult.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return document.Id;
    }
}
