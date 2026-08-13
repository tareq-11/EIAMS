using SharedKernel;

namespace Application.Abstractions.Posting;

/// <summary>
/// The single, generic strategy used whenever <c>context.Document.ReversalOfDocumentId</c> is set -
/// reversals aren't resolved through the per-DocumentType <see cref="IDocumentPostingStrategy"/>
/// registry, since a reversal shares its source's DocumentType (M3-PLAN.md §1.6). Negates the
/// source document's movements and marks the source Reversed, all inside the coordinator's single
/// posting transaction.
/// </summary>
public interface IReversalPostingStrategy
{
    Task<Result<PostingPlan>> PrepareAsync(DocumentPostingContext context, CancellationToken cancellationToken);

    Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken);
}
