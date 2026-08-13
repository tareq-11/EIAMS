using Domain.Common;
using SharedKernel;

namespace Application.Abstractions.Posting;

/// <summary>
/// A per-DocumentType posting strategy (M3-PLAN.md §1.2/§4.5). M3 defines the shared coordinator and
/// this contract only - no concrete strategy is registered yet, so every real document type resolves
/// to <c>WarehouseDocumentErrors.PostingStrategyNotAvailable</c> until M4-M7 plug theirs in. A
/// strategy must never call <c>SaveChangesAsync</c> or manage the transaction itself - the
/// coordinator owns that boundary.
/// </summary>
public interface IDocumentPostingStrategy
{
    DocumentType DocumentType { get; }

    Task<Result<PostingPlan>> PrepareAsync(DocumentPostingContext context, CancellationToken cancellationToken);

    Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken);
}
