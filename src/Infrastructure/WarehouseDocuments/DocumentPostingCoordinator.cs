using Application.Abstractions.Data;
using Application.Abstractions.Assets;
using Application.Abstractions.Ledger;
using Application.Abstractions.InventoryCounts;
using Application.Abstractions.Posting;
using Application.DocumentLines;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class DocumentPostingCoordinator(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IDocumentLock documentLock,
    IDocumentPostingScopeResolver postingScopeResolver,
    IWarehouseOperationLock warehouseOperationLock,
    IInventoryFreezePolicyService freezePolicyService,
    IInventoryLedgerWriter ledgerWriter,
    IEnumerable<IDocumentPostingStrategy> strategies,
    IEnumerable<IDocumentSubmissionValidator> submissionValidators,
    IReversalPostingStrategy reversalPostingStrategy,
    IDateTimeProvider dateTimeProvider,
    IOptions<AssetCreationOptions> assetCreationOptions) : IDocumentPostingCoordinator
{
    public Task<Result<PostingOutcome>> PostAsync(
        Guid documentId,
        int expectedRowVersion,
        Guid postedBy,
        CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            ct => PostInTransactionAsync(documentId, expectedRowVersion, postedBy, ct),
            cancellationToken);

    private async Task<Result<PostingOutcome>> PostInTransactionAsync(
        Guid documentId,
        int expectedRowVersion,
        Guid postedBy,
        CancellationToken cancellationToken)
    {
        Result<WarehouseDocument> lockResult = await documentLock.LockAsync(documentId, cancellationToken);

        if (lockResult.IsFailure)
        {
            return Result.Failure<PostingOutcome>(lockResult.Error);
        }

        WarehouseDocument document = lockResult.Value;

        if (document.RowVersion != expectedRowVersion)
        {
            return Result.Failure<PostingOutcome>(WarehouseDocumentErrors.RowVersionMismatch(
                documentId,
                expectedRowVersion,
                document.RowVersion));
        }

        Result postingGateResult = document.ValidateForPosting();

        if (postingGateResult.IsFailure)
        {
            return Result.Failure<PostingOutcome>(postingGateResult.Error);
        }

        bool hasValidSignedOriginal = await context.DocumentAttachments.AnyAsync(
            a => a.Id == document.SignedCopyAttachmentId &&
                 a.DocumentId == document.Id &&
                 a.AttachmentType == AttachmentType.SignedOriginal,
            cancellationToken);

        if (!hasValidSignedOriginal)
        {
            return Result.Failure<PostingOutcome>(WarehouseDocumentErrors.SignedCopyRequired(document.Id));
        }

        Warehouse? warehouse = await context.Warehouses
            .SingleOrDefaultAsync(w => w.Id == document.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Result.Failure<PostingOutcome>(WarehouseErrors.NotFound(document.WarehouseId));
        }

        if (warehouse.Status != Status.Active)
        {
            return Result.Failure<PostingOutcome>(WarehouseErrors.Inactive(document.WarehouseId));
        }

        if (!warehouse.CanHoldStock)
        {
            return Result.Failure<PostingOutcome>(WarehouseErrors.CannotHoldStock(document.WarehouseId));
        }

        List<DocumentLine> lines = await context.DocumentLines
            .Where(l => l.DocumentId == documentId)
            .OrderBy(l => l.CreatedAtUtc)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        Result<IReadOnlyCollection<Guid>> scopesResult = await postingScopeResolver.ResolveAsync(document, cancellationToken);
        if (scopesResult.IsFailure)
        {
            return Result.Failure<PostingOutcome>(scopesResult.Error);
        }

        await warehouseOperationLock.AcquireAsync(scopesResult.Value, cancellationToken);
        InventoryFreezeEvaluation freezeEvaluation = await freezePolicyService.EvaluateAsync(
            scopesResult.Value,
            cancellationToken);
        if (freezeEvaluation.BlockingError is not null)
        {
            return Result.Failure<PostingOutcome>(freezeEvaluation.BlockingError);
        }

        Result linesValidationResult = await DocumentLineSubmissionValidator.ValidateAsync(
            context,
            document,
            assetCreationOptions.Value,
            submissionValidators,
            cancellationToken);

        if (linesValidationResult.IsFailure)
        {
            return Result.Failure<PostingOutcome>(linesValidationResult.Error);
        }

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
                return Result.Failure<PostingOutcome>(
                    WarehouseDocumentErrors.PostingStrategyNotAvailable(documentId, document.DocumentType));
            }

            planResult = await strategy.PrepareAsync(postingContext, cancellationToken);
        }

        if (planResult.IsFailure)
        {
            return Result.Failure<PostingOutcome>(planResult.Error);
        }

        PostingPlan plan = planResult.Value;

        if (isReversal)
        {
            Result sideEffectsValidationResult = await reversalPostingStrategy.ValidateSideEffectsAsync(
                postingContext,
                cancellationToken);

            if (sideEffectsValidationResult.IsFailure)
            {
                return Result.Failure<PostingOutcome>(sideEffectsValidationResult.Error);
            }
        }

        Result ledgerResult = await ledgerWriter.AppendAsync(plan.Movements, postedBy, postedAtUtc, cancellationToken);

        if (ledgerResult.IsFailure)
        {
            return Result.Failure<PostingOutcome>(ledgerResult.Error);
        }

        Result sideEffectsResult = isReversal
            ? await reversalPostingStrategy.ApplySideEffectsAsync(postingContext, plan, cancellationToken)
            : await strategies
                .Single(s => s.DocumentType == document.DocumentType)
                .ApplySideEffectsAsync(postingContext, plan, cancellationToken);

        if (sideEffectsResult.IsFailure)
        {
            return Result.Failure<PostingOutcome>(sideEffectsResult.Error);
        }

        Result markPostedResult = document.MarkPosted(postedBy, postedAtUtc);

        if (markPostedResult.IsFailure)
        {
            return Result.Failure<PostingOutcome>(markPostedResult.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new PostingOutcome(
            document.Id,
            freezeEvaluation.Warnings
                .Select(warning => new PostingWarning(
                    warning.Code,
                    warning.Message,
                    warning.CountId,
                    warning.WarehouseId))
                .ToList());
    }
}
