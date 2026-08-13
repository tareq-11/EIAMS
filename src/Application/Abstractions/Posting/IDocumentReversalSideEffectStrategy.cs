using Domain.Common;
using Domain.WarehouseDocuments;
using SharedKernel;

namespace Application.Abstractions.Posting;

/// <summary>Applies document-type side effects while a reversal is posted in the shared transaction.</summary>
public interface IDocumentReversalSideEffectStrategy
{
    IReadOnlyCollection<DocumentType> DocumentTypes { get; }

    /// <summary>
    /// Verifies that reversal-specific side effects can be applied before any ledger movement is
    /// appended. Implementations must not mutate state from this method.
    /// </summary>
    Task<Result> ValidateAsync(
        WarehouseDocument source,
        WarehouseDocument reversal,
        CancellationToken cancellationToken);

    Task<Result> ApplyAsync(
        WarehouseDocument source,
        WarehouseDocument reversal,
        CancellationToken cancellationToken);
}
