using SharedKernel;

namespace Application.Abstractions.Posting;

/// <summary>
/// Runs the full posting pipeline for one document inside a single transaction: lock, re-validate,
/// resolve the applicable strategy (per-type or the generic reversal one), build and apply the
/// posting plan through <c>IInventoryLedgerWriter</c>, let the strategy add its side effects, then
/// mark the document Posted (M3-PLAN.md §1.2/§1.3). The only entry point into posting - no other
/// code writes StockMovement/InventoryBalance or flips a document to Posted.
/// </summary>
public interface IDocumentPostingCoordinator
{
    Task<Result<PostingOutcome>> PostAsync(
        Guid documentId,
        int expectedRowVersion,
        Guid postedBy,
        CancellationToken cancellationToken);
}
