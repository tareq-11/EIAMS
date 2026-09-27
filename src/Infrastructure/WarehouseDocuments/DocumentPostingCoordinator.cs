using System.Diagnostics;
using Application.Abstractions.Assets;
using Application.Abstractions.Data;
using Application.Abstractions.InventoryCounts;
using Application.Abstractions.Idempotency;
using Application.Abstractions.Ledger;
using Application.Abstractions.Materials;
using Application.Abstractions.Posting;
using Application.DocumentLines;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
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
    IMaterialOperationLock materialLock,
    IInventoryFreezePolicyService freezePolicyService,
    IIdempotencyService idempotencyService,
    IInventoryLedgerWriter ledgerWriter,
    IEnumerable<IDocumentPostingStrategy> strategies,
    IEnumerable<IDocumentSubmissionValidator> submissionValidators,
    IReversalPostingStrategy reversalPostingStrategy,
    IDateTimeProvider dateTimeProvider,
    IOptions<AssetCreationOptions> assetCreationOptions) : IDocumentPostingCoordinator
{
    private static readonly ActivitySource ActivitySource = new("CleanArchitecture.DocumentPosting");

    public Task<Result<PostingOutcome>> PostAsync(
        Guid documentId,
        int expectedRowVersion,
        Guid postedBy,
        CancellationToken cancellationToken) =>
        PostAsync(documentId, expectedRowVersion, postedBy, idempotencyRequest: null, cancellationToken);

    public Task<Result<PostingOutcome>> PostAsync(
        Guid documentId,
        int expectedRowVersion,
        Guid postedBy,
        IdempotencyRequest? idempotencyRequest,
        CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            ct => PostInTransactionAsync(documentId, expectedRowVersion, postedBy, idempotencyRequest, ct),
            cancellationToken);

    private async Task<Result<PostingOutcome>> PostInTransactionAsync(
        Guid documentId,
        int expectedRowVersion,
        Guid postedBy,
        IdempotencyRequest? idempotencyRequest,
        CancellationToken cancellationToken)
    {
        using Activity? activity = ActivitySource.StartActivity("PostDocument");
        activity?.SetTag("document.id", documentId);
        activity?.SetTag("posted_by.id", postedBy);

        if (idempotencyRequest is not null)
        {
            Result<IdempotencyReplay<PostingOutcome>> beginResult =
                await idempotencyService.TryBeginAsync<PostingOutcome>(
                    idempotencyRequest,
                    postedBy,
                    cancellationToken);

            if (beginResult.IsFailure)
            {
                return Result.Failure<PostingOutcome>(beginResult.Error);
            }

            if (beginResult.Value.HasResponse)
            {
                return beginResult.Value.Response!;
            }
        }

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
                 a.AttachmentType == AttachmentType.SignedOriginal &&
                 a.IsActive,
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

        // Posting interprets the live catalog for every line, so lock the same material rows a
        // classification edit locks before validating. A submitted document already counts as
        // operational use, so a concurrent classification edit is refused either way; the lock makes
        // that ordering explicit instead of relying on the editor having observed this document's
        // state, and it also serializes two posts that read a different catalog revision.
        await materialLock.AcquireAsync(
            lines.Select(line => line.MaterialId).Distinct(),
            cancellationToken);

        Result linesValidationResult = await DocumentLineSubmissionValidator.ValidateAsync(
            context,
            document,
            lines,
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

        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> affectedMaterialsByWarehouse = plan.Movements
            .GroupBy(movement => movement.WarehouseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<Guid>)group.Select(movement => movement.MaterialId).Distinct().ToArray());
        InventoryFreezeEvaluation freezeEvaluation = await freezePolicyService.EvaluateExactAsync(
            affectedMaterialsByWarehouse,
            cancellationToken);
        if (freezeEvaluation.BlockingError is not null)
        {
            return Result.Failure<PostingOutcome>(freezeEvaluation.BlockingError);
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

        var outcome = new PostingOutcome(
            document.Id,
            freezeEvaluation.Warnings
                .Select(warning => new PostingWarning(
                    warning.Code,
                    warning.Message,
                    warning.CountId,
                    warning.WarehouseId))
                .ToList());

        if (idempotencyRequest is not null)
        {
            idempotencyService.Complete(idempotencyRequest, postedBy, outcome);
        }

        await context.SaveChangesAsync(cancellationToken);

        return outcome;
    }
}
